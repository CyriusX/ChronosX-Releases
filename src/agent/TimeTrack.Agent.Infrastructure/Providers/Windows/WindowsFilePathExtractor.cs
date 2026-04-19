using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Providers;

namespace TimeTrack.Agent.Infrastructure.Providers.Windows;

/// <summary>
/// Extracts a folder path for Windows File Explorer windows.
/// For privacy and correctness, Top Folders should only include folders accessed directly in File Explorer.
/// </summary>
public sealed class WindowsFilePathExtractor : IFilePathExtractor
{
    private readonly ILogger<WindowsFilePathExtractor> _logger;

    private static readonly ConcurrentDictionary<long, ExplorerCacheEntry> _explorerPathCache = new();
    private static readonly ConcurrentDictionary<long, DateTime> _explorerUnresolvedLoggedAt = new();
    private static readonly TimeSpan ExplorerCacheTtl = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ExplorerNullCacheTtl = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ExplorerUnresolvedLogInterval = TimeSpan.FromMinutes(1);

    public WindowsFilePathExtractor(ILogger<WindowsFilePathExtractor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string? ExtractFilePath(IntPtr windowHandle, string processName, string? windowTitle)
    {
        if (windowHandle == IntPtr.Zero || string.IsNullOrEmpty(processName))
            return null;

        try
        {
            var processLower = processName.ToLowerInvariant();

            // Top Folders: only Windows File Explorer should produce a FilePath.
            if (processLower != "explorer")
                return null;

            return ExtractExplorerPath(windowHandle, windowTitle);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro ao extrair filepath para {Process}", processName);
            return null;
        }
    }

    /// <summary>
    /// Extrai caminho do Windows Explorer usando COM
    /// </summary>
    private string? ExtractExplorerPath(IntPtr windowHandle, string? windowTitle)
    {
        if (windowHandle == IntPtr.Zero)
            return null;

        // Cache hit (avoid COM enumeration every poll)
        var hwndKey = windowHandle.ToInt64();
        var now = DateTime.UtcNow;
        if (_explorerPathCache.TryGetValue(hwndKey, out var cached))
        {
            var ttl = string.IsNullOrWhiteSpace(cached.Path) ? ExplorerNullCacheTtl : ExplorerCacheTtl;
            if ((now - cached.UpdatedAtUtc) <= ttl)
                return cached.Path;
        }

        string? resolved = null;
        var matchedWindow = false;
        object? shell = null;
        object? windows = null;

        try
        {
            if (!OperatingSystem.IsWindows())
                return null;

            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null)
                return null;

            shell = Activator.CreateInstance(shellType);
            if (shell == null)
                return null;

            windows = shell.GetType().InvokeMember("Windows", System.Reflection.BindingFlags.InvokeMethod, null, shell, null);
            if (windows == null)
                return null;

            var countObj = windows.GetType().InvokeMember("Count", System.Reflection.BindingFlags.GetProperty, null, windows, null);
            var count = countObj is int i ? i : Convert.ToInt32(countObj);

            for (var idx = 0; idx < count; idx++)
            {
                object? window = null;
                try
                {
                    window = windows.GetType().InvokeMember("Item", System.Reflection.BindingFlags.InvokeMethod, null, windows, new object?[] { idx });
                    if (window == null)
                        continue;

                    var hwndObj = window.GetType().InvokeMember("HWND", System.Reflection.BindingFlags.GetProperty, null, window, null);
                    var hwnd = Convert.ToInt64(hwndObj);
                    if (hwnd != hwndKey)
                        continue;

                    matchedWindow = true;

                    // 1) Best: window.Document.Folder.Self.Path (real filesystem path when available)
                    resolved = TryGetExplorerDocumentFolderPath(window);
                    if (!string.IsNullOrWhiteSpace(resolved))
                        break;

                    // 2) Fallback: LocationURL file:///
                    resolved = TryGetExplorerLocationUrlPath(window);
                    if (!string.IsNullOrWhiteSpace(resolved))
                        break;
                }
                finally
                {
                    TryFinalReleaseComObject(window);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve Explorer folder path via Shell COM");
        }
        finally
        {
            TryFinalReleaseComObject(windows);
            TryFinalReleaseComObject(shell);
        }

        var rawCandidate = resolved;
        resolved = WindowsExplorerFolderPathNormalizer.Normalize(resolved);

        // Cache (including null) to avoid expensive COM probing
        if (_explorerPathCache.Count > 2048)
            _explorerPathCache.Clear();
        _explorerPathCache[hwndKey] = new ExplorerCacheEntry(resolved, now);

        // Log only when we actually resolved a filesystem folder (helps diagnose)
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            _logger.LogDebug("Resolved Explorer folder path: {FolderPath}", resolved);
        }
        else if (matchedWindow)
        {
            var last = _explorerUnresolvedLoggedAt.TryGetValue(hwndKey, out var t) ? t : DateTime.MinValue;
            if ((now - last) > ExplorerUnresolvedLogInterval)
            {
                _explorerUnresolvedLoggedAt[hwndKey] = now;
                _logger.LogDebug("Explorer folder path unresolved (non-filesystem location). Raw={Raw}", rawCandidate ?? "(null)");
            }
        }

        return resolved;
    }

    private static string? TryGetExplorerDocumentFolderPath(object explorerWindow)
    {
        try
        {
            var doc = explorerWindow.GetType().InvokeMember("Document", System.Reflection.BindingFlags.GetProperty, null, explorerWindow, null);
            if (doc == null) return null;

            try
            {
                var folder = doc.GetType().InvokeMember("Folder", System.Reflection.BindingFlags.GetProperty, null, doc, null);
                if (folder == null) return null;

                try
                {
                    var self = folder.GetType().InvokeMember("Self", System.Reflection.BindingFlags.GetProperty, null, folder, null);
                    if (self == null) return null;

                    try
                    {
                        var pathObj = self.GetType().InvokeMember("Path", System.Reflection.BindingFlags.GetProperty, null, self, null);
                        var path = pathObj as string ?? pathObj?.ToString();
                        return path;
                    }
                    finally
                    {
                        TryFinalReleaseComObject(self);
                    }
                }
                finally
                {
                    TryFinalReleaseComObject(folder);
                }
            }
            finally
            {
                TryFinalReleaseComObject(doc);
            }
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetExplorerLocationUrlPath(object explorerWindow)
    {
        try
        {
            var urlObj = explorerWindow.GetType().InvokeMember("LocationURL", System.Reflection.BindingFlags.GetProperty, null, explorerWindow, null);
            var url = urlObj as string ?? urlObj?.ToString();
            if (string.IsNullOrWhiteSpace(url))
                return null;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;

            if (!string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
                return null;

            // Prefer LocalPath (Windows-friendly); normalize separators below.
            return uri.LocalPath;
        }
        catch
        {
            return null;
        }
    }

    private static void TryFinalReleaseComObject(object? obj)
    {
        if (obj == null) return;

        try
        {
            if (Marshal.IsComObject(obj))
                Marshal.FinalReleaseComObject(obj);
        }
        catch
        {
            // best-effort
        }
    }

    private readonly record struct ExplorerCacheEntry(string? Path, DateTime UpdatedAtUtc);
}
