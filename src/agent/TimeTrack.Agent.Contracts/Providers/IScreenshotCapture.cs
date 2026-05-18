namespace TimeTrack.Agent.Contracts.Providers;

/// <summary>
/// Interface para captura de screenshots da janela ativa
/// </summary>
public interface IScreenshotCapture
{
    /// <summary>
    /// Captura a janela ativa como JPEG comprimido
    /// </summary>
    Task<ScreenshotResult?> CaptureActiveWindowAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Resultado de uma captura de screenshot
/// </summary>
public sealed class ScreenshotResult
{
    public required byte[] JpegData { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required string ActiveAppName { get; init; }
    public required string? ActiveWindowTitleHash { get; init; }
    public required long FileSizeBytes { get; init; }
}
