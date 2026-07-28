namespace AntiCheat.Core.Interfaces;

public interface IDesktopCaptureService
{
    string? CaptureScreenshot(string threatName = "threat");
    string? CaptureAsBase64(string threatName = "threat");
}
