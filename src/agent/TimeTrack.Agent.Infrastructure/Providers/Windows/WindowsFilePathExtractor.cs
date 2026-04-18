using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Providers;

namespace TimeTrack.Agent.Infrastructure.Providers.Windows;

/// <summary>
/// Extrai o caminho do arquivo/pasta ativo usando UIAutomation e estratégias específicas por aplicativo
/// </summary>
public sealed class WindowsFilePathExtractor : IFilePathExtractor
{
    private readonly ILogger<WindowsFilePathExtractor> _logger;

    private static readonly ConcurrentDictionary<long, ExplorerCacheEntry> _explorerPathCache = new();
    private static readonly ConcurrentDictionary<long, DateTime> _explorerUnresolvedLoggedAt = new();
    private static readonly TimeSpan ExplorerCacheTtl = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ExplorerNullCacheTtl = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ExplorerUnresolvedLogInterval = TimeSpan.FromMinutes(1);

    // Aplicativos conhecidos que expõem caminhos via UIAutomation
    private static readonly HashSet<string> AppsWithPathExtraction = new(StringComparer.OrdinalIgnoreCase)
    {
        "code", "code64", "vscode",  // VS Code
        "notepad++",                   // Notepad++
        "notepad",                     // Windows Notepad
        "wordpad",                     // WordPad
        "winword",                     // Microsoft Word
        "excel",                       // Microsoft Excel
        "powerpnt",                    // Microsoft PowerPoint
        "acrord32", "acrord64",        // Adobe Reader
        "explorer",                    // Windows Explorer
        "idea64", "idea",              // IntelliJ IDEA
        "webstorm64", "webstorm",      // WebStorm
        "rider64", "rider",            // JetBrains Rider
        "pycharm64", "pycharm",        // PyCharm
        "sublime_text",                // Sublime Text
        "atom",                        // Atom
        "obsidian",                    // Obsidian
        "typora"                       // Typora
    };

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

            // 1. Windows Explorer - usar COM para obter caminho da pasta
            if (processLower == "explorer")
            {
                return ExtractExplorerPath(windowHandle, windowTitle);
            }

            // 2. VS Code - extrair do título da janela
            if (processLower.Contains("code"))
            {
                return ExtractVsCodePath(windowTitle);
            }

            // 3. JetBrains IDEs - extrair do título
            if (IsJetBrainsIde(processLower))
            {
                return ExtractJetBrainsPath(windowTitle);
            }

            // 4. Tentar UIAutomation para outros apps
            if (AppsWithPathExtraction.Contains(processLower))
            {
                return ExtractViaUIAutomation(windowHandle, processName);
            }

            // 5. Fallback: parsing genérico do título
            return ExtractFromGenericTitle(windowTitle, processName);
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
        resolved = NormalizeExplorerFolderPath(resolved);

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

    private static string? NormalizeExplorerFolderPath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var trimmed = raw.Trim();

        // Reject virtual/non-filesystem locations like "shell:::{GUID}"
        if (!LooksLikeWindowsFileSystemPath(trimmed))
            return null;

        // Normalize separators and ensure "directory marker" trailing slash so FolderKeyNormalizer
        // treats it as a folder key rather than taking the parent of a file path.
        var normalized = trimmed.Replace('/', '\\');
        if (!normalized.EndsWith("\\", StringComparison.Ordinal))
            normalized += "\\";

        return normalized;
    }

    private static bool LooksLikeWindowsFileSystemPath(string value)
    {
        var v = value.TrimStart();

        // Explicitly reject known virtual prefixes
        if (v.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            return false;
        if (v.StartsWith("::{", StringComparison.OrdinalIgnoreCase))
            return false;

        // UNC
        if (v.StartsWith("\\\\", StringComparison.Ordinal))
            return true;

        // Drive root
        if (v.Length >= 3 && char.IsLetter(v[0]) && v[1] == ':' && (v[2] == '\\' || v[2] == '/'))
            return true;

        return false;
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

    /// <summary>
    /// Extrai caminho do VS Code do título da janela
    /// Formato: "arquivo.tsx - pasta - Visual Studio Code"
    /// </summary>
    private string? ExtractVsCodePath(string? windowTitle)
    {
        if (string.IsNullOrEmpty(windowTitle))
            return null;

        // Remove " - Visual Studio Code" ou " - VS Code" do final
        var title = windowTitle;
        var suffixes = new[] { " - Visual Studio Code", " - VS Code", " — Visual Studio Code", " — VS Code" };

        foreach (var suffix in suffixes)
        {
            var idx = title.LastIndexOf(suffix, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                title = title[..idx];
                break;
            }
        }

        // Agora temos algo como "CategoryDonut.tsx - TimeTracking"
        // O primeiro item é o arquivo, o segundo é o projeto/pasta
        var parts = title.Split(new[] { " - ", " — " }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            // Retorna "projeto/arquivo" para o backend montar o display
            return $"{parts[1].Trim()}/{parts[0].Trim()}";
        }

        if (parts.Length == 1)
        {
            return parts[0].Trim();
        }

        return title.Trim();
    }

    /// <summary>
    /// Extrai caminho de IDEs JetBrains do título
    /// Formato: "arquivo.kt – projeto – IntelliJ IDEA"
    /// </summary>
    private string? ExtractJetBrainsPath(string? windowTitle)
    {
        if (string.IsNullOrEmpty(windowTitle))
            return null;

        // JetBrains usa em dash (–) ou en dash (—)
        var separators = new[] { " – ", " — ", " - " };

        foreach (var sep in separators)
        {
            var parts = windowTitle.Split(new[] { sep }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                // Remove o nome da IDE do final se presente
                var project = parts[^1].Trim();
                var file = parts[0].Trim();

                // Verifica se o último item é o nome da IDE
                if (project.Contains("IDEA") || project.Contains("WebStorm") ||
                    project.Contains("Rider") || project.Contains("PyCharm"))
                {
                    if (parts.Length >= 3)
                    {
                        return $"{parts[^2].Trim()}/{file}";
                    }
                    return file;
                }

                return $"{project}/{file}";
            }
        }

        return windowTitle;
    }

    /// <summary>
    /// Extrai caminho via UIAutomation
    /// </summary>
    private string? ExtractViaUIAutomation(IntPtr windowHandle, string processName)
    {
        // UIAutomation pode ser lento, então usamos com moderação
        // Por ora, retorna null para usar o fallback
        // Em uma implementação futura, podemos usar UIAutomation para:
        // - Obter o conteúdo da barra de endereço do Explorer
        // - Obter o caminho da barra de título de apps que expõem

        return null;
    }

    /// <summary>
    /// Extração genérica do título
    /// </summary>
    private string? ExtractFromGenericTitle(string? windowTitle, string processName)
    {
        if (string.IsNullOrEmpty(windowTitle))
            return null;

        // Para apps que não conhecemos, retorna o título como está
        // O backend fará o parsing adequado

        // Remove sufixos comuns de browser/app
        var title = windowTitle;
        var suffixes = new[] { " - Google Chrome", " - Mozilla Firefox", " - Microsoft Edge",
                               " - Brave", " - Opera", " - Safari" };

        foreach (var suffix in suffixes)
        {
            var idx = title.LastIndexOf(suffix, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                title = title[..idx];
                break;
            }
        }

        return title.Trim();
    }

    private static bool IsJetBrainsIde(string processName)
    {
        return processName.Contains("idea") ||
               processName.Contains("webstorm") ||
               processName.Contains("rider") ||
               processName.Contains("pycharm") ||
               processName.Contains("clion") ||
               processName.Contains("goland");
    }
}
