using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Providers;

namespace TimeTrack.Agent.Infrastructure.Providers.Windows;

/// <summary>
/// Extrai o caminho do arquivo/pasta ativo usando UIAutomation e estratégias específicas por aplicativo
/// </summary>
public sealed class WindowsFilePathExtractor : IFilePathExtractor
{
    private readonly ILogger<WindowsFilePathExtractor> _logger;

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
        // O título do Explorer geralmente é o nome da pasta ou "Pasta" em português
        // Ex: "Documentos", "Downloads", "C:\Projetos\TimeTracking"

        if (string.IsNullOrEmpty(windowTitle))
            return null;

        // Se o título parece ser um caminho completo
        if (windowTitle.Length >= 2 && windowTitle[1] == ':')
        {
            return windowTitle;
        }

        // Para pastas especiais, retorna o título como está
        // O backend fará o parsing adequado
        return windowTitle;
    }

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
