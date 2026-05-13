using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Providers;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Infrastructure.Providers;

/// <summary>
/// Captura de screenshot usando System.Drawing como implementação estável.
/// Captura apenas a janela ativa (não a tela inteira) via PrintWindow.
/// Em versões futuras pode ser migrado para Windows.Graphics.Capture (WinRT).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsScreenshotCapture : IScreenshotCapture
{
    private readonly IActiveWindowProvider _activeWindowProvider;
    private readonly ILocalEncryptionService _encryptionService;
    private readonly ILogger<WindowsScreenshotCapture> _logger;

    public WindowsScreenshotCapture(
        IActiveWindowProvider activeWindowProvider,
        ILocalEncryptionService encryptionService,
        ILogger<WindowsScreenshotCapture> logger)
    {
        _activeWindowProvider = activeWindowProvider;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<ScreenshotResult?> CaptureActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var activeWindow = await _activeWindowProvider.GetActiveWindowAsync(cancellationToken);
        if (activeWindow == null)
        {
            _logger.LogDebug("No active window detected, skipping screenshot");
            return null;
        }

        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            _logger.LogDebug("No foreground window handle");
            return null;
        }

        try
        {
            var (jpegData, width, height) = CaptureWindow(hwnd);

            if (jpegData == null || jpegData.Length == 0)
            {
                _logger.LogWarning("Screenshot capture returned empty data for {App}", activeWindow.DisplayName);
                return null;
            }

            // Compress further if > 200KB
            if (jpegData.Length > 200 * 1024)
            {
                jpegData = CompressJpeg(jpegData, 40);
            }

            var titleHash = !string.IsNullOrEmpty(activeWindow.WindowTitle)
                ? ComputeSha256Hash(activeWindow.WindowTitle)
                : null;

            return new ScreenshotResult
            {
                JpegData = jpegData,
                Width = width,
                Height = height,
                ActiveAppName = activeWindow.DisplayName,
                ActiveWindowTitleHash = titleHash,
                FileSizeBytes = jpegData.Length
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture screenshot for {App}", activeWindow.DisplayName);
            return null;
        }
    }

    private static (byte[]? data, int width, int height) CaptureWindow(IntPtr hwnd)
    {
        GetWindowRect(hwnd, out var rect);
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;

        if (width <= 0 || height <= 0)
            return (null, 0, 0);

        using var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);

        graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new System.Drawing.Size(width, height),
            System.Drawing.CopyPixelOperation.SourceCopy);

        using var ms = new MemoryStream();
        var encoderParams = new System.Drawing.Imaging.EncoderParameters(1);
        encoderParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(
            System.Drawing.Imaging.Encoder.Quality, 60L);

        var codec = System.Drawing.Imaging.ImageCodecInfo.GetImageDecoders()
            .First(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);

        bitmap.Save(ms, codec, encoderParams);
        return (ms.ToArray(), width, height);
    }

    private static byte[] CompressJpeg(byte[] original, long quality)
    {
        using var inputMs = new MemoryStream(original);
        using var image = System.Drawing.Image.FromStream(inputMs);
        using var outputMs = new MemoryStream();

        var encoderParams = new System.Drawing.Imaging.EncoderParameters(1);
        encoderParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(
            System.Drawing.Imaging.Encoder.Quality, quality);

        var codec = System.Drawing.Imaging.ImageCodecInfo.GetImageDecoders()
            .First(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);

        image.Save(outputMs, codec, encoderParams);
        return outputMs.ToArray();
    }

    private static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
}
