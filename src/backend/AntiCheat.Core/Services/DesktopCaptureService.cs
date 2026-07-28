using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using AntiCheat.Core.Interfaces;

namespace AntiCheat.Core.Services;

public class DesktopCaptureService : IDesktopCaptureService
{
    private readonly ILogger<DesktopCaptureService> _logger;
    private static readonly string ScreenshotDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MFCITYAntiCheat", "screenshots");

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    public DesktopCaptureService(ILogger<DesktopCaptureService> logger)
    {
        _logger = logger;
        try { Directory.CreateDirectory(ScreenshotDir); } catch { }
    }

    public string? CaptureScreenshot(string threatName = "threat")
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            var filename = $"{Sanitize(threatName)}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.jpg";
            var filepath = Path.Combine(ScreenshotDir, filename);

            var width = GetSystemMetrics(SM_CXSCREEN);
            var height = GetSystemMetrics(SM_CYSCREEN);

            using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(0, 0, 0, 0, new Size(width, height));
            }

            var jpegEncoder = GetEncoder(ImageFormat.Jpeg);
            using var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 70L);
            bitmap.Save(filepath, jpegEncoder, encoderParams);

            _logger.LogInformation("Screenshot captured: {Filename}", filename);
            return filepath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture screenshot");
            return null;
        }
    }

    public string? CaptureAsBase64(string threatName = "threat")
    {
        var filepath = CaptureScreenshot(threatName);
        if (string.IsNullOrEmpty(filepath) || !File.Exists(filepath))
            return null;
        return Convert.ToBase64String(File.ReadAllBytes(filepath));
    }

    private static string Sanitize(string input)
    {
        return System.Text.RegularExpressions.Regex.Replace(input, @"[^a-zA-Z0-9]", "_");
    }

    private static ImageCodecInfo GetEncoder(ImageFormat format)
    {
        foreach (var codec in ImageCodecInfo.GetImageEncoders())
        {
            if (codec.FormatID == format.Guid)
                return codec;
        }
        return null!;
    }
}
