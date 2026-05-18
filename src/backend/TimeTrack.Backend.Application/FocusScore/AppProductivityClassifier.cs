using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.FocusScore;

/// <summary>
/// Classificador de produtividade de aplicações
///
/// SRP: Apenas classifica apps em categorias de produtividade
/// OCP: Listas configuráveis via constructor injection
/// </summary>
public sealed class AppProductivityClassifier
{
    // ============================================================================
    // Default App Lists (can be overridden via constructor)
    // ============================================================================

    private static readonly HashSet<string> DefaultProductiveApps = new(StringComparer.OrdinalIgnoreCase)
    {
        // IDEs and Code Editors
        "code", "vscode", "visual studio code", "idea", "intellij", "rider", "webstorm",
        "pycharm", "clion", "goland", "android studio", "xcode", "sublime", "atom",
        "notepad++", "vim", "nvim", "neovim", "emacs", "nano",

        // Design Tools
        "figma", "sketch", "adobe xd", "photoshop", "illustrator", "after effects",
        "premiere pro", "blender", "inkscape", "gimp",

        // Office/Productivity
        "word", "excel", "powerpoint", "outlook", "onenote", "notion", "obsidian",
        "onenote", "evernote", "typora", "marktext", "slack", "teams", "zoom",
        "meet", "discord", "mattermost",

        // Terminal/Dev Tools
        "terminal", "cmd", "powershell", "windowsterminal", "iterm", "hyper",
        "alacritty", "kitty", "putty", "winscp", "filezilla", "git", "sourcetree",
        "postman", "insomnia", "docker", "kubernetes",

        // Database Tools
        "datagrip", "dbeaver", "pgadmin", "mysql workbench", "sql server management studio",
        "robo 3t", "mongodb compass", "redisinsight",

        // Documentation
        "docs", "confluence", "readme", "gitbook", "swagger"
    };

    private static readonly HashSet<string> DefaultDistractionApps = new(StringComparer.OrdinalIgnoreCase)
    {
        // Social Media
        "instagram", "facebook", "twitter", "tiktok", "threads", "mastodon",
        "linkedin", "pinterest", "reddit", "tumblr",

        // Video/Streaming
        "youtube", "netflix", "amazon prime", "hulu", "disney+", "hbo max",
        "twitch", "vimeo", "dailymotion",

        // Games
        "steam", "epic games", "origin", "uplay", "battlenet", "gog",
        "league of legends", "valorant", "csgo", "dota", "minecraft",
        "fortnite", "apex legends", "overwatch",

        // Music/Podcast (when not work-related)
        "spotify", "applemusic", "itunes", "soundcloud", "tidal", "deezer",

        // Messaging (non-work)
        "whatsapp", "telegram", "messenger", "signal", "viber", "line",
        "wechat", "kakaotalk", "snapchat"
    };

    private static readonly HashSet<string> DefaultNeutralApps = new(StringComparer.OrdinalIgnoreCase)
    {
        // Browsers (context-dependent)
        "chrome", "firefox", "edge", "safari", "opera", "brave",
        "chromium", "vivaldi", "arc", "brave browser", "google chrome",
        "microsoft edge", "mozillafirefox",

        // File Managers
        "explorer", "finder", "total commander", "doublecmd",

        // System Tools
        "task manager", "activity monitor", "system preferences", "settings"
    };

    // ============================================================================
    // Instance Fields
    // ============================================================================

    private readonly HashSet<string> _productiveApps;
    private readonly HashSet<string> _distractionApps;
    private readonly HashSet<string> _neutralApps;
    private readonly HashSet<string> _excludedApps;

    // ============================================================================
    // Constructor
    // ============================================================================

    /// <summary>
    /// Creates a classifier with default app lists
    /// </summary>
    public AppProductivityClassifier() : this(null, null, null, null)
    {
    }

    /// <summary>
    /// Creates a classifier with custom app lists
    /// </summary>
    public AppProductivityClassifier(
        IEnumerable<string>? productiveApps,
        IEnumerable<string>? distractionApps,
        IEnumerable<string>? neutralApps,
        IEnumerable<string>? excludedApps)
    {
        _productiveApps = productiveApps != null
            ? new HashSet<string>(productiveApps, StringComparer.OrdinalIgnoreCase)
            : DefaultProductiveApps;

        _distractionApps = distractionApps != null
            ? new HashSet<string>(distractionApps, StringComparer.OrdinalIgnoreCase)
            : DefaultDistractionApps;

        _neutralApps = neutralApps != null
            ? new HashSet<string>(neutralApps, StringComparer.OrdinalIgnoreCase)
            : DefaultNeutralApps;

        _excludedApps = excludedApps != null
            ? new HashSet<string>(excludedApps, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    // ============================================================================
    // Classification Methods
    // ============================================================================

    /// <summary>
    /// Classifica uma aplicação pelo nome do processo
    /// </summary>
    /// <param name="processName">Nome do processo (sem .exe)</param>
    /// <returns>Categoria de produtividade</returns>
    public AppProductivityCategory Classify(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return AppProductivityCategory.Neutral;

        // Remove .exe extension if present
        var cleanName = processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase)
                                   .Trim();

        // Check if excluded
        if (_excludedApps.Contains(cleanName))
            return AppProductivityCategory.Neutral;

        // Check each category
        if (IsProductive(cleanName))
            return AppProductivityCategory.Productive;

        if (IsDistraction(cleanName))
            return AppProductivityCategory.Distraction;

        if (IsNeutral(cleanName))
            return AppProductivityCategory.Neutral;

        // Default: check partial matches
        return ClassifyByPartialMatch(cleanName);
    }

    /// <summary>
    /// Classifica uma aplicação considerando o contexto (alias for ClassifyWithContext)
    /// </summary>
    public AppProductivityCategory ClassifyWithContext(string processName, string? windowTitle)
    {
        var baseCategory = Classify(processName);

        // For browsers, try to infer from window title
        if (baseCategory == AppProductivityCategory.Neutral &&
            IsBrowser(processName) &&
            !string.IsNullOrWhiteSpace(windowTitle))
        {
            return ClassifyBrowserByTitle(windowTitle);
        }

        return baseCategory;
    }

    // ============================================================================
    // Private Methods
    // ============================================================================

    private bool IsProductive(string processName)
    {
        return _productiveApps.Contains(processName) ||
               ContainsAnyKeyword(processName, _productiveApps);
    }

    private bool IsDistraction(string processName)
    {
        return _distractionApps.Contains(processName) ||
               ContainsAnyKeyword(processName, _distractionApps);
    }

    private bool IsNeutral(string processName)
    {
        return _neutralApps.Contains(processName);
    }

    private bool IsBrowser(string processName)
    {
        return _neutralApps.Contains(processName) &&
               (processName.Contains("chrome", StringComparison.OrdinalIgnoreCase) ||
                processName.Contains("firefox", StringComparison.OrdinalIgnoreCase) ||
                processName.Contains("edge", StringComparison.OrdinalIgnoreCase) ||
                processName.Contains("safari", StringComparison.OrdinalIgnoreCase));
    }

    private AppProductivityCategory ClassifyBrowserByTitle(string windowTitle)
    {
        var title = windowTitle.ToLowerInvariant();

        // Productive patterns
        if (title.Contains("github") ||
            title.Contains("gitlab") ||
            title.Contains("stackoverflow") ||
            title.Contains("stack overflow") ||
            title.Contains("docs.") ||
            title.Contains("documentation") ||
            title.Contains("jira") ||
            title.Contains("confluence") ||
            title.Contains("notion") ||
            title.Contains("figma") ||
            title.Contains("localhost") ||
            title.Contains("127.0.0.1"))
        {
            return AppProductivityCategory.Productive;
        }

        // Distraction patterns
        if (title.Contains("youtube") ||
            title.Contains("netflix") ||
            title.Contains("facebook") ||
            title.Contains("instagram") ||
            title.Contains("twitter") ||
            title.Contains("tiktok") ||
            title.Contains("reddit") ||
            title.Contains("twitch"))
        {
            return AppProductivityCategory.Distraction;
        }

        return AppProductivityCategory.Neutral;
    }

    private AppProductivityCategory ClassifyByPartialMatch(string processName)
    {
        var lower = processName.ToLowerInvariant();

        // Check for common productive patterns
        if (lower.Contains("code") || lower.Contains("ide") || lower.Contains("studio"))
            return AppProductivityCategory.Productive;

        // Check for common distraction patterns
        if (lower.Contains("game") || lower.Contains("play"))
            return AppProductivityCategory.Distraction;

        return AppProductivityCategory.Neutral;
    }

    private static bool ContainsAnyKeyword(string processName, HashSet<string> keywords)
    {
        var lower = processName.ToLowerInvariant();
        foreach (var keyword in keywords)
        {
            if (lower.Contains(keyword.ToLowerInvariant()))
                return true;
        }
        return false;
    }
}
