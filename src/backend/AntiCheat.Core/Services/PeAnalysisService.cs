using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using AntiCheat.Core.Interfaces;
using AntiCheat.Core.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class PeAnalysisService : IPeAnalysisService
{
    private readonly ILogger<PeAnalysisService> _logger;

    public PeAnalysisService(ILogger<PeAnalysisService> logger)
    {
        _logger = logger;
    }

    public Task<PeAnalysisResult> AnalyzeAsync(string filePath, CancellationToken ct = default)
    {
        return Task.Run(() => AnalyzeInternal(filePath, null, ct), ct);
    }

    public Task<PeAnalysisResult> AnalyzeAsync(byte[] fileData, string? filePath = null, CancellationToken ct = default)
    {
        return Task.Run(() => AnalyzeInternal(filePath, fileData, ct), ct);
    }

    private PeAnalysisResult AnalyzeInternal(string? filePath, byte[]? fileData, CancellationToken ct)
    {
        var result = new PeAnalysisResult();

        try
        {
            byte[] data;
            if (fileData != null)
            {
                data = fileData;
                result.FilePath = filePath ?? "(memory)";
            }
            else if (filePath != null && File.Exists(filePath))
            {
                data = File.ReadAllBytes(filePath);
                result.FilePath = filePath;
            }
            else
            {
                result.ParseError = "File not found or no data provided";
                return result;
            }

            result.FileSize = data.Length;
            ct.ThrowIfCancellationRequested();

            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            ParseDosHeader(reader, result);
            if (!result.DosHeader.Valid)
            {
                result.ParseError = "Invalid DOS header (MZ magic not found)";
                return result;
            }

            ParseNtHeaders(reader, result);
            if (!result.NtHeader.Valid)
            {
                result.ParseError = "Invalid NT header (PE\\0\\0 signature not found)";
                return result;
            }

            ParseFileHeader(reader, result);
            ParseOptionalHeader(reader, result);
            ParseSections(reader, result);

            ct.ThrowIfCancellationRequested();

            Parallel.Invoke(
                () => ParseImports(data, reader, result),
                () => ParseExports(data, result),
                () => ParseResources(data, result),
                () => ParseSignature(filePath, result),
                () => ParseRichHeader(data, result.NtHeader.Offset, result),
                () => ParseDebug(data, result),
                () => DetectClr(data, result),
                () => ParseTls(data, result),
                () => ParseOverlay(data, result),
                () => CalculateHashes(data, result),
                () => DetectPacker(result),
                () => CalculateEntropy(data, result)
            );

            result.ParsingSucceeded = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.ParseError = $"Analysis failed: {ex.Message}";
            _logger.LogWarning(ex, "PE analysis failed for {Path}", result.FilePath);
        }

        return result;
    }

    private void ParseDosHeader(BinaryReader reader, PeAnalysisResult result)
    {
        var magic = reader.ReadUInt16();
        reader.BaseStream.Seek(0x3C, SeekOrigin.Begin);
        var lfaNew = reader.ReadUInt32();

        result.DosHeader.Magic = magic;
        result.DosHeader.LfaNew = lfaNew;
    }

    private void ParseNtHeaders(BinaryReader reader, PeAnalysisResult result)
    {
        reader.BaseStream.Seek(result.DosHeader.LfaNew, SeekOrigin.Begin);
        var sig = reader.ReadUInt32();
        result.NtHeader.Signature = sig;
        result.NtHeader.Offset = result.DosHeader.LfaNew;
    }

    private void ParseFileHeader(BinaryReader reader, PeAnalysisResult result)
    {
        var fh = result.FileHeader;
        fh.Machine = reader.ReadUInt16();
        fh.NumberOfSections = reader.ReadUInt16();
        fh.TimeDateStamp = reader.ReadUInt32();
        fh.PointerToSymbolTable = reader.ReadUInt32();
        fh.NumberOfSymbols = reader.ReadUInt32();
        fh.SizeOfOptionalHeader = reader.ReadUInt16();
        fh.Characteristics = reader.ReadUInt16();

        var ch = fh.Characteristics;
        if ((ch & 0x0001) != 0) fh.CharacteristicsFlags.Add("RELOCS_STRIPPED");
        if ((ch & 0x0002) != 0) fh.CharacteristicsFlags.Add("EXECUTABLE_IMAGE");
        if ((ch & 0x0004) != 0) fh.CharacteristicsFlags.Add("LINE_NUMS_STRIPPED");
        if ((ch & 0x0008) != 0) fh.CharacteristicsFlags.Add("LOCAL_SYMS_STRIPPED");
        if ((ch & 0x0010) != 0) fh.CharacteristicsFlags.Add("AGGRESSIVE_WS_TRIM");
        if ((ch & 0x0020) != 0) fh.CharacteristicsFlags.Add("LARGE_ADDRESS_AWARE");
        if ((ch & 0x0080) != 0) fh.CharacteristicsFlags.Add("BYTES_REVERSED_LO");
        if ((ch & 0x0100) != 0) fh.CharacteristicsFlags.Add("32BIT_MACHINE");
        if ((ch & 0x0200) != 0) fh.CharacteristicsFlags.Add("DEBUG_STRIPPED");
        if ((ch & 0x0400) != 0) fh.CharacteristicsFlags.Add("REMOVABLE_RUN_FROM_SWAP");
        if ((ch & 0x0800) != 0) fh.CharacteristicsFlags.Add("NET_RUN_FROM_SWAP");
        if ((ch & 0x1000) != 0) fh.CharacteristicsFlags.Add("SYSTEM");
        if ((ch & 0x2000) != 0) fh.CharacteristicsFlags.Add("DLL");
        if ((ch & 0x4000) != 0) fh.CharacteristicsFlags.Add("UP_SYSTEM_ONLY");
        if ((ch & 0x8000) != 0) fh.CharacteristicsFlags.Add("BYTES_REVERSED_HI");
    }

    private void ParseOptionalHeader(BinaryReader reader, PeAnalysisResult result)
    {
        var oh = result.OptionalHeader;
        var basePos = reader.BaseStream.Position;

        oh.Magic = reader.ReadUInt16();
        oh.MajorLinkerVersion = reader.ReadByte();
        oh.MinorLinkerVersion = reader.ReadByte();
        oh.SizeOfCode = reader.ReadUInt32();
        oh.SizeOfInitializedData = reader.ReadUInt32();
        oh.SizeOfUninitializedData = reader.ReadUInt32();
        oh.AddressOfEntryPoint = reader.ReadUInt32();
        oh.BaseOfCode = reader.ReadUInt32();

        if (!oh.IsPe32Plus)
        {
            oh.ImageBase = reader.ReadUInt32();
        }
        else
        {
            oh.ImageBase = reader.ReadUInt64();
        }

        oh.SectionAlignment = reader.ReadUInt32();
        oh.FileAlignment = reader.ReadUInt32();
        oh.MajorOperatingSystemVersion = reader.ReadUInt16();
        oh.MinorOperatingSystemVersion = reader.ReadUInt16();
        oh.MajorImageVersion = reader.ReadUInt16();
        oh.MinorImageVersion = reader.ReadUInt16();
        oh.MajorSubsystemVersion = reader.ReadUInt16();
        oh.MinorSubsystemVersion = reader.ReadUInt16();
        oh.SizeOfImage = reader.ReadUInt32();
        oh.SizeOfHeaders = reader.ReadUInt32();

        if (!oh.IsPe32Plus)
        {
            oh.Subsystem = reader.ReadUInt32();
            oh.DllCharacteristics = reader.ReadUInt16();
            oh.SizeOfStackReserve = reader.ReadUInt32();
            oh.SizeOfStackCommit = reader.ReadUInt32();
            oh.SizeOfHeapReserve = reader.ReadUInt32();
            oh.SizeOfHeapCommit = reader.ReadUInt32();
        }
        else
        {
            oh.Subsystem = reader.ReadUInt32();
            oh.DllCharacteristics = reader.ReadUInt16();
            oh.SizeOfStackReserve = reader.ReadUInt64();
            oh.SizeOfStackCommit = reader.ReadUInt64();
            oh.SizeOfHeapReserve = reader.ReadUInt64();
            oh.SizeOfHeapCommit = reader.ReadUInt64();
        }

        oh.NumberOfRvaAndSizes = reader.ReadUInt32();

        var dc = oh.DllCharacteristics;
        if ((dc & 0x0001) != 0) oh.DllCharacteristicsFlags.Add("RESERVED_1");
        if ((dc & 0x0020) != 0) oh.DllCharacteristicsFlags.Add("HIGH_ENTROPY_VA");
        if ((dc & 0x0040) != 0) oh.DllCharacteristicsFlags.Add("DYNAMIC_BASE");
        if ((dc & 0x0080) != 0) oh.DllCharacteristicsFlags.Add("FORCE_INTEGRITY");
        if ((dc & 0x0100) != 0) oh.DllCharacteristicsFlags.Add("NX_COMPAT");
        if ((dc & 0x0200) != 0) oh.DllCharacteristicsFlags.Add("NO_ISOLATION");
        if ((dc & 0x0400) != 0) oh.DllCharacteristicsFlags.Add("NO_SEH");
        if ((dc & 0x0800) != 0) oh.DllCharacteristicsFlags.Add("NO_BIND");
        if ((dc & 0x1000) != 0) oh.DllCharacteristicsFlags.Add("APPCONTAINER");
        if ((dc & 0x2000) != 0) oh.DllCharacteristicsFlags.Add("WDM_DRIVER");
        if ((dc & 0x4000) != 0) oh.DllCharacteristicsFlags.Add("GUARD_CF");
        if ((dc & 0x8000) != 0) oh.DllCharacteristicsFlags.Add("TERMINAL_SERVER_AWARE");
    }

    private void ParseSections(BinaryReader reader, PeAnalysisResult result)
    {
        var count = result.FileHeader.NumberOfSections;
        if (count > 100)
        {
            _logger.LogWarning("Suspicious section count: {Count}", count);
            count = 100;
        }

        for (int i = 0; i < count; i++)
        {
            var rawName = reader.ReadBytes(8);
            var name = Encoding.UTF8.GetString(rawName).TrimEnd('\0');
            var vsize = reader.ReadUInt32();
            var vaddr = reader.ReadUInt32();
            var rsize = reader.ReadUInt32();
            var roffset = reader.ReadUInt32();
            reader.ReadBytes(12);
            var chars = reader.ReadUInt32();

            result.Sections.Add(new SectionInfo
            {
                Name = name,
                VirtualSize = vsize,
                VirtualAddress = vaddr,
                RawSize = rsize,
                RawOffset = roffset,
                Characteristics = chars,
            });
        }
    }

    private void ParseImports(byte[] data, BinaryReader reader, PeAnalysisResult result)
    {
        try
        {
            var importDir = GetDataDirectory(data, 1);
            if (importDir.Size == 0) return;

            var isPe32Plus = result.OptionalHeader.IsPe32Plus;
            var importTable = ReadDirectoryData(data, importDir);
            if (importTable == null) return;

            using var ims = new MemoryStream(importTable);
            using var imReader = new BinaryReader(ims);

            var dangerousApis = new Dictionary<string, (string Category, string Dll)>
            {
                ["OpenProcess"] = ("Process Manipulation", "kernel32"),
                ["WriteProcessMemory"] = ("Memory Write", "kernel32"),
                ["VirtualAllocEx"] = ("Memory Allocation", "kernel32"),
                ["VirtualProtectEx"] = ("Memory Protection", "kernel32"),
                ["CreateRemoteThread"] = ("Remote Thread", "kernel32"),
                ["SetWindowsHookEx"] = ("Hooking", "user32"),
                ["LoadLibraryA"] = ("DLL Loading", "kernel32"),
                ["LoadLibraryW"] = ("DLL Loading", "kernel32"),
                ["LoadLibraryExA"] = ("DLL Loading", "kernel32"),
                ["LoadLibraryExW"] = ("DLL Loading", "kernel32"),
                ["LdrLoadDll"] = ("DLL Loading", "ntdll"),
                ["NtWriteVirtualMemory"] = ("Memory Write", "ntdll"),
                ["NtMapViewOfSection"] = ("Memory Mapping", "ntdll"),
                ["ZwProtectVirtualMemory"] = ("Memory Protection", "ntdll"),
                ["CreateToolhelp32Snapshot"] = ("Process Enumeration", "kernel32"),
                ["ReadProcessMemory"] = ("Memory Read", "kernel32"),
                ["NtUnmapViewOfSection"] = ("Memory Unmapping", "ntdll"),
                ["GetProcAddress"] = ("Dynamic API Resolution", "kernel32"),
                ["QueueUserAPC"] = ("APC Injection", "kernel32"),
                ["NtQueueApcThread"] = ("APC Injection", "ntdll"),
                ["NtCreateThreadEx"] = ("Remote Thread", "ntdll"),
                ["RtlCreateUserThread"] = ("Remote Thread", "ntdll"),
                ["SetThreadContext"] = ("Thread Manipulation", "kernel32"),
                ["NtSetContextThread"] = ("Thread Manipulation", "ntdll"),
                ["SuspendThread"] = ("Thread Manipulation", "kernel32"),
                ["NtSuspendThread"] = ("Thread Manipulation", "ntdll"),
                ["ResumeThread"] = ("Thread Manipulation", "kernel32"),
                ["NtResumeThread"] = ("Thread Manipulation", "ntdll"),
                ["VirtualAlloc"] = ("Memory Allocation", "kernel32"),
                ["VirtualProtect"] = ("Memory Protection", "kernel32"),
                ["HeapCreate"] = ("Heap Manipulation", "kernel32"),
                ["WriteProcessMemory"] = ("Memory Write", "kernel32"),
            };

            while (true)
            {
                var thunkSize = isPe32Plus ? 8u : 4u;
                var importDescSize = 20u;

                if (ims.Position + importDescSize > ims.Length) break;

                var originalFirstThunk = isPe32Plus ? imReader.ReadUInt64() : imReader.ReadUInt32();
                var timeDateStamp = imReader.ReadUInt32();
                var forwarderChain = imReader.ReadUInt32();
                var nameRva = imReader.ReadUInt32();
                var firstThunk = isPe32Plus ? imReader.ReadUInt64() : imReader.ReadUInt32();

                if (nameRva == 0) break;

                var dllName = ReadRvaString(data, nameRva);
                if (string.IsNullOrEmpty(dllName))
                {
                    if (originalFirstThunk == 0 && firstThunk == 0) break;
                    continue;
                }

                dllName = Path.GetFileNameWithoutExtension(dllName).ToLowerInvariant();
                result.Imports.Dlls.Add(dllName);

                var apiNames = new List<string>();
                var thunkRva = originalFirstThunk != 0
                    ? (uint)(isPe32Plus ? originalFirstThunk : (uint)originalFirstThunk)
                    : (uint)(isPe32Plus ? firstThunk : (uint)firstThunk);

                if (thunkRva == 0) continue;

                var thunkData = ReadDirectoryData(data, new DataDirectoryEntry { Rva = thunkRva, Size = 0x1000 });
                if (thunkData == null) continue;

                using var ts = new MemoryStream(thunkData);
                using var tr = new BinaryReader(ts);

                while (true)
                {
                    ulong entry;
                    if (isPe32Plus)
                    {
                        if (ts.Position + 8 > ts.Length) break;
                        entry = tr.ReadUInt64();
                    }
                    else
                    {
                        if (ts.Position + 4 > ts.Length) break;
                        entry = tr.ReadUInt32();
                    }

                    if (entry == 0) break;

                    if ((entry & (isPe32Plus ? 0x8000000000000000UL : 0x80000000UL)) != 0)
                    {
                        var ordinal = entry & 0x7FFFFFFF;
                        apiNames.Add($"ORDINAL_{ordinal}");
                        continue;
                    }

                    var apiName = ReadRvaString(data, (uint)(entry & 0x7FFFFFFF));
                    if (!string.IsNullOrEmpty(apiName))
                    {
                        apiNames.Add(apiName);

                        if (dangerousApis.TryGetValue(apiName, out var info))
                        {
                            result.Imports.DangerousImports.Add(new DangerousImport
                            {
                                ApiName = apiName,
                                DllName = dllName,
                                Category = info.Category,
                            });
                        }
                    }
                }

                result.Imports.Imports[dllName] = apiNames;
            }

            result.Imports.Imports = result.Imports.Imports
                .OrderBy(kv => kv.Key)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Import analysis failed");
        }
    }

    private void ParseExports(byte[] data, PeAnalysisResult result)
    {
        try
        {
            var exportDir = GetDataDirectory(data, 0);
            if (exportDir.Size == 0) return;

            var exportData = ReadDirectoryData(data, exportDir);
            if (exportData == null || exportData.Length < 40) return;

            using var ms = new MemoryStream(exportData);
            using var r = new BinaryReader(ms);

            var exports = new ExportInfo
            {
                ExportFlags = r.ReadUInt32(),
                Timestamp = r.ReadUInt32(),
                MajorVersion = r.ReadUInt16(),
                MinorVersion = r.ReadUInt16(),
                NameRva = r.ReadUInt32(),
                Base = r.ReadUInt32(),
                NumberOfFunctions = r.ReadUInt32(),
                NumberOfNames = r.ReadUInt32(),
            };

            exports.Name = ReadRvaString(data, exports.NameRva);

            var funcRva = r.ReadUInt32();
            var nameRva = r.ReadUInt32();
            var ordinalRva = r.ReadUInt32();

            var funcData = ReadDirectoryData(data, new DataDirectoryEntry { Rva = funcRva, Size = exports.NumberOfFunctions * 4 });
            var nameData = ReadDirectoryData(data, new DataDirectoryEntry { Rva = nameRva, Size = exports.NumberOfNames * 4 });
            var ordData = ReadDirectoryData(data, new DataDirectoryEntry { Rva = ordinalRva, Size = exports.NumberOfNames * 2 });

            if (funcData != null && nameData != null && ordData != null)
            {
                using var nms = new MemoryStream(nameData);
                using var nr = new BinaryReader(nms);
                using var oms = new MemoryStream(ordData);
                using var or = new BinaryReader(oms);

                var nameRvas = new List<uint>();
                for (int i = 0; i < exports.NumberOfNames; i++)
                    nameRvas.Add(nr.ReadUInt32());

                var ordinals = new List<ushort>();
                for (int i = 0; i < exports.NumberOfNames; i++)
                    ordinals.Add(or.ReadUInt16());

                for (int i = 0; i < exports.NumberOfNames && i < ordinals.Count; i++)
                {
                    var fname = ReadRvaString(data, nameRvas[i]);
                    if (!string.IsNullOrEmpty(fname))
                        exports.FunctionNames.Add(fname);
                }

                using var fms = new MemoryStream(funcData);
                using var fr = new BinaryReader(fms);
                for (int i = 0; i < exports.NumberOfFunctions; i++)
                {
                    var addr = fr.ReadUInt32();
                    if (addr >= exportDir.Rva && addr < exportDir.Rva + exportDir.Size)
                    {
                        var forwarded = ReadRvaString(data, addr);
                        if (!string.IsNullOrEmpty(forwarded))
                            exports.ForwardedFunctions.Add(forwarded);
                    }
                }
            }

            result.Exports = exports;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Export analysis failed");
        }
    }

    private void ParseResources(byte[] data, PeAnalysisResult result)
    {
        try
        {
            var resDir = GetDataDirectory(data, 2);
            if (resDir.Size == 0) return;

            result.Resources.HasResources = true;
            var resBytes = ReadDirectoryData(data, resDir);
            if (resBytes == null) return;

            ParseVersionInfo(data, result);
            ParseManifest(data, result);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Resource analysis failed");
        }
    }

    private void ParseVersionInfo(byte[] data, PeAnalysisResult result)
    {
        try
        {
            var resDir = GetDataDirectory(data, 2);
            if (resDir.Size == 0) return;

            var versionRva = FindResourceEntry(data, resDir, 16, 1);
            if (versionRva == null) return;

            var versionBytes = ReadDirectoryData(data, new DataDirectoryEntry { Rva = versionRva.Value, Size = 0x10000 });
            if (versionBytes == null || versionBytes.Length < 4) return;

            using var ms = new MemoryStream(versionBytes);
            using var r = new BinaryReader(ms);

            var vi = new VersionInfo();
            while (ms.Position + 4 <= ms.Length)
            {
                var len = r.ReadUInt16();
                var valLen = r.ReadUInt16();
                var type = r.ReadUInt16();
                if (len == 0) break;

                var key = ReadSz(r);
                if (string.IsNullOrEmpty(key)) break;

                r.BaseStream.Seek(ms.Position + (ms.Position % 4 == 0 ? 0 : 4 - ms.Position % 4), SeekOrigin.Begin);

                if (key == "StringFileInfo")
                {
                    ParseStringTable(versionBytes, ms, r, vi);
                    break;
                }
            }

            if (vi.CompanyName != null || vi.ProductName != null)
                result.Resources.Version = vi;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Version info parsing failed");
        }
    }

    private void ParseStringTable(byte[] data, MemoryStream ms, BinaryReader r, VersionInfo vi)
    {
        try
        {
            if (ms.Position + 4 > ms.Length) return;
            var slen = r.ReadUInt16();
            var svalLen = r.ReadUInt16();
            var stype = r.ReadUInt16();
            var skey = ReadSz(r);
            if (string.IsNullOrEmpty(skey)) return;

            AlignStream(ms);
            if (ms.Position + 4 > ms.Length) return;
            var clen = r.ReadUInt16();
            var cvalLen = r.ReadUInt16();
            var ctype = r.ReadUInt16();
            var ckey = ReadSz(r);
            if (string.IsNullOrEmpty(ckey) || ckey != "StringTable") return;

            AlignStream(ms);
            while (ms.Position + 4 <= ms.Length)
            {
                long startPos = ms.Position;
                var entryLen = r.ReadUInt16();
                if (entryLen == 0) break;
                var entryValLen = r.ReadUInt16();
                var entryType = r.ReadUInt16();
                var entryKey = ReadSz(r);

                AlignStream(ms);
                var entryValue = ReadSz(r);

                switch (entryKey)
                {
                    case "CompanyName": vi.CompanyName = entryValue; break;
                    case "ProductName": vi.ProductName = entryValue; break;
                    case "ProductVersion": vi.ProductVersion = entryValue; break;
                    case "FileVersion": vi.FileVersion = entryValue; break;
                    case "OriginalFilename": vi.OriginalFilename = entryValue; break;
                    case "FileDescription": vi.FileDescription = entryValue; break;
                    case "LegalCopyright": vi.LegalCopyright = entryValue; break;
                    case "InternalName": vi.InternalName = entryValue; break;
                    case "PrivateBuild": vi.PrivateBuild = entryValue; break;
                    case "SpecialBuild": vi.SpecialBuild = entryValue; break;
                }

                ms.Position = startPos + entryLen;
            }
        }
        catch { }
    }

    private void ParseManifest(byte[] data, PeAnalysisResult result)
    {
        try
        {
            var resDir = GetDataDirectory(data, 2);
            if (resDir.Size == 0) return;

            var manifestRva = FindResourceEntry(data, resDir, 24, 1);
            if (manifestRva == null) return;

            var manifestBytes = ReadDirectoryData(data, new DataDirectoryEntry { Rva = manifestRva.Value, Size = 0x10000 });
            if (manifestBytes == null) return;

            result.Resources.Manifest = Encoding.UTF8.GetString(manifestBytes).TrimEnd('\0');
        }
        catch { }
    }

    private void ParseSignature(string? filePath, PeAnalysisResult result)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            result.Signature = new SignatureInfo { IsSigned = false };
            return;
        }

        try
        {
            var cert = X509Certificate.CreateFromSignedFile(filePath) as X509Certificate2;
            if (cert == null)
            {
                result.Signature = new SignatureInfo { IsSigned = false };
                return;
            }

            var sig = new SignatureInfo
            {
                IsSigned = true,
                Subject = cert.Subject,
                Issuer = cert.Issuer,
                Thumbprint = cert.Thumbprint,
                NotAfter = cert.NotAfter,
                NotBefore = cert.NotBefore,
            };

            sig.IsSelfSigned = string.Equals(sig.Subject, sig.Issuer, StringComparison.OrdinalIgnoreCase);

            try
            {
                var chain = new X509Chain { ChainPolicy = { RevocationMode = X509RevocationMode.Offline } };
                chain.Build(cert);
                sig.ChainStatus = string.Join("; ", chain.ChainStatus.Select(s => s.Status.ToString()));
                sig.IsRevoked = chain.ChainStatus.Any(s =>
                    s.Status == X509ChainStatusFlags.Revoked ||
                    s.Status == X509ChainStatusFlags.RevocationStatusUnknown);
            }
            catch
            {
                sig.ChainStatus = "Chain verification failed (offline)";
            }

            result.Signature = sig;
        }
        catch (Exception ex)
        {
            result.Signature = new SignatureInfo
            {
                IsSigned = false,
                ChainStatus = $"Signature read failed: {ex.Message}",
            };
        }
    }

    private void ParseRichHeader(byte[] data, uint ntHeaderOffset, PeAnalysisResult result)
    {
        try
        {
            var rich = new RichHeaderInfo();

            const uint richSignature = 0x68636952;
            for (long i = ntHeaderOffset - 8; i >= 0x80; i -= 4)
            {
                if (BitConverter.ToUInt32(data, (int)i) == richSignature)
                {
                    rich.Present = true;
                    rich.Offset = (uint)i;

                    var xorKey = BitConverter.ToUInt32(data, (int)i + 4);
                    var richStart = 0x80u;
                    var richLen = i - richStart;

                    if (richLen > 0 && richLen <= 4096)
                    {
                        rich.Length = (uint)richLen;
                        var decrypted = new byte[richLen];
                        Buffer.BlockCopy(data, (int)richStart, decrypted, 0, (int)richLen);

                        for (int j = 0; j < richLen; j += 4)
                        {
                            if (j + 4 <= richLen)
                            {
                                var val = BitConverter.ToUInt32(decrypted, j) ^ xorKey;
                                BitConverter.GetBytes(val).CopyTo(decrypted, j);
                            }
                        }

                        using var ms = new MemoryStream(decrypted);
                        using var r = new BinaryReader(ms);

                        while (ms.Position + 8 <= ms.Length)
                        {
                            var id = r.ReadUInt16();
                            var build = r.ReadUInt16();
                            var count = r.ReadUInt32();
                            if (id == 0 && build == 0 && count == 0) break;

                            rich.Entries.Add(new RichHeaderEntry
                            {
                                ProductId = id,
                                BuildId = build,
                                Count = count,
                            });
                        }

                        MapRichHeaderProducts(rich);

                        using var sha256 = System.Security.Cryptography.SHA256.Create();
                        var hash = sha256.ComputeHash(decrypted);
                        rich.Hash = BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
                    }
                    break;
                }
            }

            result.RichHeader = rich;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Rich header parsing failed");
        }
    }

    private static void MapRichHeaderProducts(RichHeaderInfo rich)
    {
        var products = new Dictionary<ushort, (string Product, string Tool)>
        {
            { 0x0000, ("Unknown", "Unknown") },
            { 0x0001, ("Import0", "Import Library") },
            { 0x0002, ("Linker510", "Linker 5.10") },
            { 0x0003, ("Cvtomf510", "Cvtomf 5.10") },
            { 0x0004, ("Linker600", "Linker 6.00") },
            { 0x0005, ("Cvtomf600", "Cvtomf 6.00") },
            { 0x0006, ("Linker700", "Linker 7.00") },
            { 0x0007, ("Cvtomf700", "Cvtomf 7.00") },
            { 0x0008, ("Linker800", "Linker 8.00") },
            { 0x0009, ("Cvtomf800", "Cvtomf 8.00") },
            { 0x000A, ("Cvtomf900", "Cvtomf 9.00") },
            { 0x000B, ("Linker900", "Linker 9.00") },
            { 0x000C, ("Export0", "Export Library") },
            { 0x000D, ("Export1", "Export Library") },
            { 0x000E, ("Export2", "Export Library") },
            { 0x000F, ("Import1", "Import Library") },
            { 0x0010, ("Import2", "Import Library") },
            { 0x0011, ("Import3", "Import Library") },
            { 0x0012, ("Import4", "Import Library") },
            { 0x0013, ("Import5", "Import Library") },
            { 0x0014, ("Import6", "Import Library") },
            { 0x0015, ("Import7", "Import Library") },
            { 0x0016, ("Import8", "Import Library") },
            { 0x0017, ("Import9", "Import Library") },
            { 0x0018, ("Import10", "Import Library") },
            { 0x0019, ("Import11", "Import Library") },
            { 0x001A, ("Import12", "Import Library") },
            { 0x001B, ("Cvtres510", "Cvtres 5.10") },
            { 0x001C, ("Cvtres600", "Cvtres 6.00") },
            { 0x001D, ("Cvtres700", "Cvtres 7.00") },
            { 0x001E, ("Cvtres800", "Cvtres 8.00") },
            { 0x001F, ("Cvtres900", "Cvtres 9.00") },
            { 0x0020, ("Reserved0", "Resource") },
            { 0x0021, ("Reserved1", "Resource") },
            { 0x0022, ("Reserved2", "Resource") },
            { 0x0023, ("Reserved3", "Resource") },
            { 0x0024, ("Reserved4", "Resource") },
            { 0x0025, ("Reserved5", "Resource") },
            { 0x0026, ("Reserved6", "Resource") },
            { 0x0027, ("Reserved7", "Resource") },
            { 0x0028, ("Reserved8", "Resource") },
            { 0x0029, ("Reserved9", "Resource") },
            { 0x002A, ("Reserved10", "Resource") },
            { 0x002B, ("Reserved11", "Resource") },
            { 0x002C, ("Reserved12", "Resource") },
            { 0x002D, ("Reserved13", "Resource") },
            { 0x002E, ("Reserved14", "Resource") },
            { 0x002F, ("Reserved15", "Resource") },
            { 0x0030, ("Reserved16", "Resource") },
            { 0x0031, ("Reserved17", "Resource") },
            { 0x0032, ("Reserved18", "Resource") },
            { 0x0033, ("Reserved19", "Resource") },
            { 0x0034, ("Reserved20", "Resource") },
            { 0x0035, ("Reserved21", "Resource") },
            { 0x0036, ("Reserved22", "Resource") },
            { 0x0037, ("Reserved23", "Resource") },
            { 0x0038, ("Reserved24", "Resource") },
            { 0x0039, ("Reserved25", "Resource") },
            { 0x003A, ("Reserved26", "Resource") },
            { 0x003B, ("Reserved27", "Resource") },
            { 0x003C, ("Reserved28", "Resource") },
            { 0x003D, ("Reserved29", "Resource") },
            { 0x003E, ("Reserved30", "Resource") },
            { 0x003F, ("Reserved31", "Resource") },
            { 0x0040, ("Reserved32", "Resource") },
            { 0x0041, ("Reserved33", "Resource") },
            { 0x0042, ("Reserved34", "Resource") },
            { 0x0043, ("Reserved35", "Resource") },
            { 0x0044, ("Reserved36", "Resource") },
            { 0x0045, ("Reserved37", "Resource") },
            { 0x0046, ("Reserved38", "Resource") },
            { 0x0047, ("Reserved39", "Resource") },
            { 0x0048, ("Reserved40", "Resource") },
            { 0x0049, ("Reserved41", "Resource") },
            { 0x004A, ("Reserved42", "Resource") },
            { 0x004B, ("Reserved43", "Resource") },
            { 0x004C, ("Reserved44", "Resource") },
            { 0x004D, ("Reserved45", "Resource") },
            { 0x004E, ("Reserved46", "Resource") },
            { 0x004F, ("Reserved47", "Resource") },
            { 0x0050, ("Reserved48", "Resource") },
            { 0x0051, ("Reserved49", "Resource") },
            { 0x0052, ("Reserved50", "Resource") },
            { 0x0053, ("Reserved51", "Resource") },
            { 0x0054, ("Reserved52", "Resource") },
            { 0x0055, ("Reserved53", "Resource") },
            { 0x0056, ("Reserved54", "Resource") },
            { 0x0057, ("Reserved55", "Resource") },
            { 0x0058, ("Reserved56", "Resource") },
            { 0x0059, ("Reserved57", "Resource") },
            { 0x005A, ("Reserved58", "Resource") },
            { 0x005B, ("Reserved59", "Resource") },
            { 0x005C, ("Reserved60", "Resource") },
            { 0x005D, ("Reserved61", "Resource") },
            { 0x005E, ("Reserved62", "Resource") },
            { 0x005F, ("Reserved63", "Resource") },
            { 0x0060, ("Reserved64", "Resource") },
            { 0x0061, ("Reserved65", "Resource") },
            { 0x0062, ("Reserved66", "Resource") },
            { 0x0063, ("Reserved67", "Resource") },
            { 0x0064, ("Reserved68", "Resource") },
            { 0x0065, ("Reserved69", "Resource") },
            { 0x0066, ("Reserved70", "Resource") },
            { 0x0067, ("Reserved71", "Resource") },
            { 0x0068, ("Reserved72", "Resource") },
            { 0x0069, ("Reserved73", "Resource") },
            { 0x006A, ("Reserved74", "Resource") },
            { 0x006B, ("Reserved75", "Resource") },
            { 0x006C, ("Reserved76", "Resource") },
            { 0x006D, ("Reserved77", "Resource") },
            { 0x006E, ("Reserved78", "Resource") },
            { 0x006F, ("Reserved79", "Resource") },
            { 0x0070, ("Reserved80", "Resource") },
            { 0x0071, ("Reserved81", "Resource") },
            { 0x0072, ("Reserved82", "Resource") },
            { 0x0073, ("Reserved83", "Resource") },
            { 0x0074, ("Reserved84", "Resource") },
            { 0x0075, ("Reserved85", "Resource") },
            { 0x0076, ("Reserved86", "Resource") },
            { 0x0077, ("Reserved87", "Resource") },
            { 0x0078, ("Reserved88", "Resource") },
            { 0x0079, ("Reserved89", "Resource") },
            { 0x007A, ("Reserved90", "Resource") },
            { 0x007B, ("Reserved91", "Resource") },
            { 0x007C, ("Reserved92", "Resource") },
            { 0x007D, ("Reserved93", "Resource") },
            { 0x007E, ("Reserved94", "Resource") },
            { 0x007F, ("Reserved95", "Resource") },
            { 0x0080, ("Reserved96", "Resource") },
            { 0x0081, ("Reserved97", "Resource") },
            { 0x0082, ("Reserved98", "Resource") },
            { 0x0083, ("Reserved99", "Resource") },
            { 0x0084, ("Reserved100", "Resource") },
            { 0x0085, ("Reserved101", "Resource") },
            { 0x0086, ("Reserved102", "Resource") },
            { 0x0087, ("Reserved103", "Resource") },
            { 0x0088, ("Reserved104", "Resource") },
            { 0x0089, ("Reserved105", "Resource") },
            { 0x008A, ("Reserved106", "Resource") },
            { 0x008B, ("Reserved107", "Resource") },
            { 0x008C, ("Reserved108", "Resource") },
            { 0x008D, ("Reserved109", "Resource") },
            { 0x008E, ("Reserved110", "Resource") },
            { 0x008F, ("Reserved111", "Resource") },
            { 0x0090, ("Reserved112", "Resource") },
            { 0x0091, ("Reserved113", "Resource") },
            { 0x0092, ("Reserved114", "Resource") },
            { 0x0093, ("Reserved115", "Resource") },
            { 0x0094, ("Reserved116", "Resource") },
            { 0x0095, ("Reserved117", "Resource") },
            { 0x0096, ("Reserved118", "Resource") },
            { 0x0097, ("Reserved119", "Resource") },
            { 0x0098, ("Reserved120", "Resource") },
            { 0x0099, ("Reserved121", "Resource") },
            { 0x009A, ("Reserved122", "Resource") },
            { 0x009B, ("Reserved123", "Resource") },
            { 0x009C, ("Reserved124", "Resource") },
            { 0x009D, ("Reserved125", "Resource") },
            { 0x009E, ("Reserved126", "Resource") },
            { 0x009F, ("Reserved127", "Resource") },
            { 0x00A0, ("Reserved128", "Resource") },
            { 0x00A1, ("Reserved129", "Resource") },
            { 0x00A2, ("Reserved130", "Resource") },
            { 0x00A3, ("Reserved131", "Resource") },
            { 0x00A4, ("Reserved132", "Resource") },
            { 0x00A5, ("Reserved133", "Resource") },
            { 0x00A6, ("Reserved134", "Resource") },
            { 0x00A7, ("Reserved135", "Resource") },
            { 0x00A8, ("Reserved136", "Resource") },
            { 0x00A9, ("Reserved137", "Resource") },
            { 0x00AA, ("Reserved138", "Resource") },
            { 0x00AB, ("Reserved139", "Resource") },
            { 0x00AC, ("Reserved140", "Resource") },
            { 0x00AD, ("Reserved141", "Resource") },
            { 0x00AE, ("Reserved142", "Resource") },
            { 0x00AF, ("Reserved143", "Resource") },
            { 0x00B0, ("Reserved144", "Resource") },
            { 0x00B1, ("Reserved145", "Resource") },
            { 0x00B2, ("Reserved146", "Resource") },
            { 0x00B3, ("Reserved147", "Resource") },
            { 0x00B4, ("Reserved148", "Resource") },
            { 0x00B5, ("Reserved149", "Resource") },
            { 0x00B6, ("Reserved150", "Resource") },
            { 0x00B7, ("Reserved151", "Resource") },
            { 0x00B8, ("Reserved152", "Resource") },
            { 0x00B9, ("Reserved153", "Resource") },
            { 0x00BA, ("Reserved154", "Resource") },
            { 0x00BB, ("Reserved155", "Resource") },
            { 0x00BC, ("Reserved156", "Resource") },
            { 0x00BD, ("Reserved157", "Resource") },
            { 0x00BE, ("Reserved158", "Resource") },
            { 0x00BF, ("Reserved159", "Resource") },
            { 0x00C0, ("Reserved160", "Resource") },
            { 0x00C1, ("Reserved161", "Resource") },
            { 0x00C2, ("Reserved162", "Resource") },
            { 0x00C3, ("Reserved163", "Resource") },
            { 0x00C4, ("Reserved164", "Resource") },
            { 0x00C5, ("Reserved165", "Resource") },
            { 0x00C6, ("Reserved166", "Resource") },
            { 0x00C7, ("Reserved167", "Resource") },
            { 0x00C8, ("Reserved168", "Resource") },
            { 0x00C9, ("Reserved169", "Resource") },
            { 0x00CA, ("Reserved170", "Resource") },
            { 0x00CB, ("Reserved171", "Resource") },
            { 0x00CC, ("Reserved172", "Resource") },
            { 0x00CD, ("Reserved173", "Resource") },
            { 0x00CE, ("Reserved174", "Resource") },
            { 0x00CF, ("Reserved175", "Resource") },
            { 0x00D0, ("Reserved176", "Resource") },
            { 0x00D1, ("Reserved177", "Resource") },
            { 0x00D2, ("Reserved178", "Resource") },
            { 0x00D3, ("Reserved179", "Resource") },
            { 0x00D4, ("Reserved180", "Resource") },
            { 0x00D5, ("Reserved181", "Resource") },
            { 0x00D6, ("Reserved182", "Resource") },
            { 0x00D7, ("Reserved183", "Resource") },
            { 0x00D8, ("Reserved184", "Resource") },
            { 0x00D9, ("Reserved185", "Resource") },
            { 0x00DA, ("Reserved186", "Resource") },
            { 0x00DB, ("Reserved187", "Resource") },
            { 0x00DC, ("Reserved188", "Resource") },
            { 0x00DD, ("Reserved189", "Resource") },
            { 0x00DE, ("Reserved190", "Resource") },
            { 0x00DF, ("Reserved191", "Resource") },
            { 0x00E0, ("Reserved192", "Resource") },
            { 0x00E1, ("Reserved193", "Resource") },
            { 0x00E2, ("Reserved194", "Resource") },
            { 0x00E3, ("Reserved195", "Resource") },
            { 0x00E4, ("Reserved196", "Resource") },
            { 0x00E5, ("Reserved197", "Resource") },
            { 0x00E6, ("Reserved198", "Resource") },
            { 0x00E7, ("Reserved199", "Resource") },
            { 0x00E8, ("Reserved200", "Resource") },
            { 0x00E9, ("Reserved201", "Resource") },
            { 0x00EA, ("Reserved202", "Resource") },
            { 0x00EB, ("Reserved203", "Resource") },
            { 0x00EC, ("Reserved204", "Resource") },
            { 0x00ED, ("Reserved205", "Resource") },
            { 0x00EE, ("Reserved206", "Resource") },
            { 0x00EF, ("Reserved207", "Resource") },
            { 0x00F0, ("Reserved208", "Resource") },
            { 0x00F1, ("Reserved209", "Resource") },
            { 0x00F2, ("Reserved210", "Resource") },
            { 0x00F3, ("Reserved211", "Resource") },
            { 0x00F4, ("Reserved212", "Resource") },
            { 0x00F5, ("Reserved213", "Resource") },
            { 0x00F6, ("Reserved214", "Resource") },
            { 0x00F7, ("Reserved215", "Resource") },
            { 0x00F8, ("Reserved216", "Resource") },
            { 0x00F9, ("Reserved217", "Resource") },
            { 0x00FA, ("Reserved218", "Resource") },
            { 0x00FB, ("Reserved219", "Resource") },
            { 0x00FC, ("Reserved220", "Resource") },
            { 0x00FD, ("Reserved221", "Resource") },
            { 0x00FE, ("Reserved222", "Resource") },
            { 0x00FF, ("Reserved223", "Resource") },
        };

        foreach (var entry in rich.Entries)
        {
            if (products.TryGetValue(entry.ProductId, out var info))
            {
                entry.ProductName = info.Product;
                entry.ToolDescription = info.Tool;
            }
            else
            {
                entry.ProductName = $"Unknown (0x{entry.ProductId:X4})";
                entry.ToolDescription = "Unknown Tool";
            }
        }
    }

    private void ParseDebug(byte[] data, PeAnalysisResult result)
    {
        try
        {
            var debugDir = GetDataDirectory(data, 6);
            if (debugDir.Size == 0) return;

            result.Debug.HasDebugDirectory = true;
            var debugBytes = ReadDirectoryData(data, debugDir);
            if (debugBytes == null || debugBytes.Length < 28) return;

            using var ms = new MemoryStream(debugBytes);
            using var r = new BinaryReader(ms);

            while (ms.Position + 28 <= ms.Length)
            {
                var characteristics = r.ReadUInt32();
                var timeDateStamp = r.ReadUInt32();
                var majorVersion = r.ReadUInt16();
                var minorVersion = r.ReadUInt16();
                var type = r.ReadUInt32();
                var sizeOfData = r.ReadUInt32();
                var addressOfRawData = r.ReadUInt32();
                var pointerToRawData = r.ReadUInt32();

                if (type == 2 && pointerToRawData > 0 && sizeOfData > 0)
                {
                    if (pointerToRawData + sizeOfData <= data.Length)
                    {
                        var cvBytes = new byte[sizeOfData];
                        Buffer.BlockCopy(data, (int)pointerToRawData, cvBytes, 0, (int)sizeOfData);
                        ParseCodeView(cvBytes, result);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Debug directory parsing failed");
        }
    }

    private static void ParseCodeView(byte[] cvData, PeAnalysisResult result)
    {
        using var ms = new MemoryStream(cvData);
        using var r = new BinaryReader(ms);

        var sig = r.ReadUInt32();
        if (sig == 0x53445352)
        {
            var guidBytes = r.ReadBytes(16);
            var age = r.ReadUInt32();

            result.Debug.Guid = new Guid(guidBytes).ToString("D").ToUpperInvariant();
            result.Debug.Age = age;
            result.Debug.DebugType = "RSDS (PDB 7.0)";

            if (ms.Position < cvData.Length)
            {
                var pdbBytes = new byte[cvData.Length - ms.Position];
                Buffer.BlockCopy(cvData, (int)ms.Position, pdbBytes, 0, pdbBytes.Length);
                result.Debug.PdbPath = Encoding.UTF8.GetString(pdbBytes).TrimEnd('\0');
            }
        }
        else if (sig == 0x5344534E)
        {
            result.Debug.DebugType = "NB10 (PDB 2.0)";
            var offset = r.ReadUInt32();
            var sig2 = r.ReadUInt32();
            var age = r.ReadUInt32();

            if (ms.Position < cvData.Length)
            {
                var pdbBytes = new byte[cvData.Length - ms.Position];
                Buffer.BlockCopy(cvData, (int)ms.Position, pdbBytes, 0, pdbBytes.Length);
                result.Debug.PdbPath = Encoding.UTF8.GetString(pdbBytes).TrimEnd('\0');
            }
        }
    }

    private void DetectClr(byte[] data, PeAnalysisResult result)
    {
        try
        {
            var clrDir = GetDataDirectory(data, 14);
            if (clrDir.Size == 0)
            {
                result.Clr.Type = ClrType.Native;
                return;
            }

            var clrBytes = ReadDirectoryData(data, clrDir);
            if (clrBytes == null || clrBytes.Length < 24) return;

            using var ms = new MemoryStream(clrBytes);
            using var r = new BinaryReader(ms);

            result.Clr.ClrMajor = r.ReadUInt16();
            result.Clr.ClrMinor = r.ReadUInt16();
            result.Clr.ClrMajor = r.ReadUInt16();
            result.Clr.ClrMinor = r.ReadUInt16();
            result.Clr.ClrFlags = r.ReadUInt32();
            result.Clr.ClrMetaDataRva = r.ReadUInt32();
            result.Clr.ClrMetaDataSize = r.ReadUInt32();

            var isNativeOnly = (result.Clr.ClrFlags & 0x00000001) != 0;
            var isMixedMode = (result.Clr.ClrFlags & 0x00000008) != 0;

            if (isMixedMode)
                result.Clr.Type = ClrType.NetMixedMode;
            else if (!isNativeOnly)
                result.Clr.Type = ClrType.NetAssembly;
            else
                result.Clr.Type = ClrType.Native;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "CLR detection failed");
        }
    }

    private void ParseTls(byte[] data, PeAnalysisResult result)
    {
        try
        {
            var tlsDir = GetDataDirectory(data, 9);
            if (tlsDir.Size == 0) return;

            var isPe32Plus = result.OptionalHeader.IsPe32Plus;
            var tlsBytes = ReadDirectoryData(data, tlsDir);
            if (tlsBytes == null) return;

            using var ms = new MemoryStream(tlsBytes);
            using var r = new BinaryReader(ms);

            ulong callbacksRva;
            if (isPe32Plus)
            {
                var rawDataStartVA = r.ReadUInt64();
                var rawDataEndVA = r.ReadUInt64();
                var tlsIndex = r.ReadUInt64();
                callbacksRva = r.ReadUInt64();
                var sizeOfZeroFill = r.ReadUInt32();
                var characteristics = r.ReadUInt32();
            }
            else
            {
                var rawDataStartVA = r.ReadUInt32();
                var rawDataEndVA = r.ReadUInt32();
                var tlsIndex = r.ReadUInt32();
                callbacksRva = r.ReadUInt32();
                var sizeOfZeroFill = r.ReadUInt32();
                var characteristics = r.ReadUInt32();
            }

            if (callbacksRva == 0) return;

            result.Tls.HasTls = true;
            var cbFileOffset = RvaToFileOffset(data, (uint)callbacksRva, result);
            if (cbFileOffset == null) return;

            using var cbMs = new MemoryStream(data);
            using var cbR = new BinaryReader(cbMs);
            cbMs.Seek(cbFileOffset.Value, SeekOrigin.Begin);

            while (true)
            {
                ulong callback;
                if (isPe32Plus)
                {
                    if (cbMs.Position + 8 > data.Length) break;
                    callback = cbR.ReadUInt64();
                }
                else
                {
                    if (cbMs.Position + 4 > data.Length) break;
                    callback = cbR.ReadUInt32();
                }

                if (callback == 0) break;
                result.Tls.CallbackAddresses.Add(callback);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "TLS parsing failed");
        }
    }

    private void ParseOverlay(byte[] data, PeAnalysisResult result)
    {
        try
        {
            long lastSectionEnd = 0;
            foreach (var section in result.Sections)
            {
                var sectionEnd = section.RawOffset + section.RawSize;
                if (sectionEnd > lastSectionEnd)
                    lastSectionEnd = sectionEnd;
            }

            if (lastSectionEnd < data.Length)
            {
                result.Overlay.Exists = true;
                result.Overlay.Offset = lastSectionEnd;
                result.Overlay.Size = data.Length - lastSectionEnd;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Overlay analysis failed");
        }
    }

    private void CalculateHashes(byte[] data, PeAnalysisResult result)
    {
        try
        {
            using var sha256 = SHA256.Create();
            result.Hashes.Sha256 = BitConverter.ToString(sha256.ComputeHash(data)).Replace("-", "").ToUpperInvariant();

            using var sha1 = SHA1.Create();
            result.Hashes.Sha1 = BitConverter.ToString(sha1.ComputeHash(data)).Replace("-", "").ToUpperInvariant();

            using var md5 = MD5.Create();
            result.Hashes.Md5 = BitConverter.ToString(md5.ComputeHash(data)).Replace("-", "").ToUpperInvariant();

            ComputeImpHash(result);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Hash calculation failed");
        }
    }

    private void ComputeImpHash(PeAnalysisResult result)
    {
        try
        {
            if (result.Imports.Imports.Count == 0) return;
            var sb = new StringBuilder();
            foreach (var kvp in result.Imports.Imports.OrderBy(k => k.Key))
            {
                foreach (var api in kvp.Value.OrderBy(a => a))
                {
                    sb.Append(api.ToLowerInvariant());
                    sb.Append(',');
                }
            }

            if (sb.Length > 0)
            {
                using var md5 = MD5.Create();
                var hash = md5.ComputeHash(Encoding.ASCII.GetBytes(sb.ToString()));
                result.Hashes.ImpHash = BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ImpHash calculation failed");
        }
    }

    private void DetectPacker(PeAnalysisResult result)
    {
        try
        {
            var sectionNames = result.Sections.Select(s => s.Name.ToUpperInvariant()).ToList();
            var allNames = string.Join(" ", sectionNames);
            var detections = new List<string>();
            string? packerName = null;
            double confidence = 0;

            if (sectionNames.Any(n => n.StartsWith("UPX")))
            {
                detections.Add("UPX section naming");
                packerName = "UPX";
                confidence = 0.9;
            }
            else if (allNames.Contains("THEMIDA") || allNames.Contains("SFL"))
            {
                detections.Add("Themida/SecureForce section naming");
                packerName = "Themida";
                confidence = 0.85;
            }
            else if (sectionNames.Any(n => n.StartsWith(".VMP")))
            {
                detections.Add("VMProtect section naming");
                packerName = "VMProtect";
                confidence = 0.9;
            }
            else if (sectionNames.Contains(".MPRESS1") || sectionNames.Contains(".MPRESS2"))
            {
                detections.Add("MPRESS section naming");
                packerName = "MPRESS";
                confidence = 0.9;
            }
            else if (sectionNames.Contains(".ADATA"))
            {
                detections.Add("ASPack section naming");
                packerName = "ASPack";
                confidence = 0.85;
            }
            else if (sectionNames.Contains(".PETITE"))
            {
                detections.Add("PEtite section naming");
                packerName = "PEtite";
                confidence = 0.9;
            }
            else if (sectionNames.Contains(".PCLE") || sectionNames.Contains(".PCL2"))
            {
                detections.Add("PECompact section naming");
                packerName = "PECompact";
                confidence = 0.85;
            }
            else if (sectionNames.Contains(".ENIGMA1") || sectionNames.Contains(".ENIGMA"))
            {
                detections.Add("Enigma Protector section naming");
                packerName = "Enigma Protector";
                confidence = 0.9;
            }
            else
            {
                var highEntropySections = result.Sections.Count(s => s.Entropy > 7.0);
                var hasWxSection = result.Sections.Any(s => s.IsExecutable && s.IsWritable);
                var rawVsVirtual = result.Sections.Any(s =>
                    s.RawSize > 0 && s.VirtualSize > 0 &&
                    (double)s.RawSize / s.VirtualSize < 0.3);

                if (highEntropySections >= 2 && hasWxSection && rawVsVirtual)
                {
                    detections.Add($"High entropy ({highEntropySections} sections >7.0)");
                    detections.Add("Executable+Writable section present");
                    detections.Add("Compressed-like section ratio");
                    packerName = "Unknown Packer";
                    confidence = 0.6;
                }
                else if (highEntropySections >= 3)
                {
                    detections.Add($"{highEntropySections} high-entropy sections");
                    packerName = "Suspicious (possibly packed)";
                    confidence = 0.4;
                }
            }

            result.Packer.IsPacked = packerName != null;
            result.Packer.PackerName = packerName;
            result.Packer.Confidence = confidence;
            result.Packer.DetectedSignatures = detections;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Packer detection failed");
        }
    }

    private void CalculateEntropy(byte[] data, PeAnalysisResult result)
    {
        try
        {
            result.Entropy.FileEntropy = ShannonEntropy(data, 0, data.Length);

            double maxSec = 0, minSec = double.MaxValue;
            string? maxSecName = null;
            foreach (var section in result.Sections)
            {
                if (section.RawSize > 0 && section.RawOffset + section.RawSize <= data.Length)
                {
                    var secEntropy = ShannonEntropy(data, (int)section.RawOffset, (int)section.RawSize);
                    section.Entropy = secEntropy;

                    if (secEntropy > maxSec) { maxSec = secEntropy; maxSecName = section.Name; }
                    if (secEntropy < minSec) minSec = secEntropy;
                }
            }

            result.Entropy.MaxSectionEntropy = maxSec > 0 ? maxSec : null;
            result.Entropy.MinSectionEntropy = minSec < double.MaxValue ? minSec : null;
            result.Entropy.HighestEntropySection = maxSecName;

            if (result.Overlay.Exists && result.Overlay.Size > 0)
            {
                var overlayEnd = result.Overlay.Offset + result.Overlay.Size;
                if (overlayEnd <= data.Length)
                {
                    result.Entropy.OverlayEntropy = ShannonEntropy(data, (int)result.Overlay.Offset, (int)result.Overlay.Size);
                    result.Overlay.Entropy = result.Entropy.OverlayEntropy;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Entropy calculation failed");
        }
    }

    private static double ShannonEntropy(byte[] data, int offset, int length)
    {
        if (length == 0) return 0;
        var freq = new int[256];
        for (int i = offset; i < offset + length && i < data.Length; i++)
            freq[data[i]]++;

        double entropy = 0;
        for (int i = 0; i < 256; i++)
        {
            if (freq[i] == 0) continue;
            double p = (double)freq[i] / length;
            entropy -= p * Math.Log2(p);
        }
        return Math.Round(entropy, 4);
    }

    private DataDirectoryEntry GetDataDirectory(byte[] data, int index)
    {
        var ntOffset = BitConverter.ToUInt32(data, 0x3C);
        var isPe32Plus = BitConverter.ToUInt16(data, (int)ntOffset + 24) == 0x020B;

        var dirOffset = ntOffset + 24 + (isPe32Plus ? 112u : 96u);
        var entryOffset = dirOffset + (uint)(index * 8);

        if (entryOffset + 8 > data.Length)
            return new DataDirectoryEntry();

        return new DataDirectoryEntry
        {
            Rva = BitConverter.ToUInt32(data, (int)entryOffset),
            Size = BitConverter.ToUInt32(data, (int)entryOffset + 4),
        };
    }

    private static byte[]? ReadDirectoryData(byte[] data, DataDirectoryEntry dir)
    {
        if (dir.Size == 0 || dir.Rva == 0) return null;

        var sections = ParseSectionHeaders(data, BitConverter.ToUInt32(data, 0x3C));
        var fileOffset = RvaToFileOffset(data, dir.Rva, sections);
        if (fileOffset == null) return null;

        var readSize = Math.Min(dir.Size, (uint)(data.Length - fileOffset.Value));
        if (readSize == 0) return null;

        var result = new byte[readSize];
        Buffer.BlockCopy(data, (int)fileOffset.Value, result, 0, (int)readSize);
        return result;
    }

    private static uint? FindResourceEntry(byte[] data, DataDirectoryEntry resDir, int typeId, int nameId)
    {
        var resBytes = ReadDirectoryData(data, resDir);
        if (resBytes == null) return null;

        using var ms = new MemoryStream(resBytes);
        using var r = new BinaryReader(ms);

        var firstLevel = FindResourceLevel(r, resBytes, typeId);
        if (firstLevel == null) return null;

        var resDirData = ReadDirectoryData(data, new DataDirectoryEntry { Rva = firstLevel.Value, Size = 0x10000 });
        if (resDirData == null) return null;

        using var ms2 = new MemoryStream(resDirData);
        using var r2 = new BinaryReader(ms2);

        var secondLevel = FindResourceLevel(r2, resDirData, nameId);
        if (secondLevel == null) return null;

        var thirdLevel = ReadDirectoryData(data, new DataDirectoryEntry { Rva = secondLevel.Value, Size = 0x10000 });
        if (thirdLevel == null || thirdLevel.Length < 16) return null;

        var dataEntryRva = BitConverter.ToUInt32(thirdLevel, 0);
        return dataEntryRva;
    }

    private static uint? FindResourceLevel(BinaryReader r, byte[] data, int searchId)
    {
        var namedCount = r.ReadUInt16();
        var idCount = r.ReadUInt16();
        r.ReadBytes(4);

        for (int i = 0; i < namedCount + idCount; i++)
        {
            if (r.BaseStream.Position + 8 > data.Length) return null;
            var id = r.ReadUInt32();
            var offset = r.ReadUInt32();

            if ((id & 0x80000000) == 0 && id == searchId)
            {
                if ((offset & 0x80000000) != 0)
                    return offset & 0x7FFFFFFF;
                return offset;
            }
        }
        return null;
    }

    private uint? RvaToFileOffset(byte[] data, uint rva, PeAnalysisResult result)
    {
        foreach (var section in result.Sections)
        {
            if (rva >= section.VirtualAddress && rva < section.VirtualAddress + Math.Max(section.VirtualSize, section.RawSize))
            {
                return section.RawOffset + (rva - section.VirtualAddress);
            }
        }
        return null;
    }

    private static List<SectionHeader> ParseSectionHeaders(byte[] data, uint ntHeaderOffset)
    {
        var sections = new List<SectionHeader>();
        var numSections = BitConverter.ToUInt16(data, (int)ntHeaderOffset + 6);
        var isPe32Plus = BitConverter.ToUInt16(data, (int)ntHeaderOffset + 24) == 0x020B;
        var optHeaderSize = isPe32Plus ? 240 : 224;
        var firstSection = ntHeaderOffset + 24 + (uint)optHeaderSize;

        for (int i = 0; i < numSections && i < 100; i++)
        {
            var offset = firstSection + (uint)(i * 40);
            if (offset + 40 > data.Length) break;

            sections.Add(new SectionHeader
            {
                VirtualAddress = BitConverter.ToUInt32(data, (int)offset + 12),
                RawOffset = BitConverter.ToUInt32(data, (int)offset + 20),
                RawSize = BitConverter.ToUInt32(data, (int)offset + 16),
            });
        }
        return sections;
    }

    private static uint? RvaToFileOffset(byte[] data, uint rva, List<SectionHeader> sections)
    {
        foreach (var section in sections)
        {
            if (rva >= section.VirtualAddress && rva < section.VirtualAddress + section.RawSize)
            {
                return section.RawOffset + (rva - section.VirtualAddress);
            }
        }
        return null;
    }

    private static string? ReadRvaString(byte[] data, uint rva)
    {
        var sections = ParseSectionHeaders(data, BitConverter.ToUInt32(data, 0x3C));
        var fileOffset = RvaToFileOffset(data, rva, sections);
        if (fileOffset == null || fileOffset.Value >= data.Length) return null;

        int maxLen = (int)Math.Min(256, data.Length - fileOffset.Value);
        int len = 0;
        while (len < maxLen && data[fileOffset.Value + len] != 0) len++;

        return Encoding.ASCII.GetString(data, (int)fileOffset.Value, len);
    }

    private static string ReadSz(BinaryReader r)
    {
        var bytes = new List<byte>();
        try
        {
            while (true)
            {
                var b = r.ReadByte();
                if (b == 0) break;
                bytes.Add(b);
            }
        }
        catch { }
        return Encoding.Unicode.GetString(bytes.ToArray());
    }

    private static void AlignStream(MemoryStream ms)
    {
        var align = ms.Position % 4;
        if (align != 0)
            ms.Seek(4 - align, SeekOrigin.Current);
    }

    private struct DataDirectoryEntry
    {
        public uint Rva;
        public uint Size;
    }

    private struct SectionHeader
    {
        public uint VirtualAddress;
        public uint RawOffset;
        public uint RawSize;
    }
}
