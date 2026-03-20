using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Application.UseCases.RecordActiveWindow;

/// <summary>
/// Maps application exe names / display names to productivity categories.
/// Tries an exact exe-name match first, then falls back to display-name substring.
/// </summary>
internal static class AppCategorizer
{
    // Key = exe name without extension, lowercase
    private static readonly Dictionary<string, AppCategory> ByExeName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // ── Development tools ────────────────────────────────────────────
            ["code"]               = AppCategory.Productive("development"),
            ["code - insiders"]    = AppCategory.Productive("development"),
            ["devenv"]             = AppCategory.Productive("development"),  // Visual Studio
            ["rider64"]            = AppCategory.Productive("development"),
            ["idea64"]             = AppCategory.Productive("development"),  // IntelliJ
            ["webstorm64"]         = AppCategory.Productive("development"),
            ["pycharm64"]          = AppCategory.Productive("development"),
            ["clion64"]            = AppCategory.Productive("development"),
            ["datagrip64"]         = AppCategory.Productive("development"),
            ["androidstudio"]      = AppCategory.Productive("development"),
            ["android studio"]     = AppCategory.Productive("development"),
            ["xcode"]              = AppCategory.Productive("development"),
            ["wt"]                 = AppCategory.Productive("development"),  // Windows Terminal
            ["windowsterminal"]    = AppCategory.Productive("development"),
            ["powershell"]         = AppCategory.Productive("development"),
            ["powershell_ise"]     = AppCategory.Productive("development"),
            ["cmd"]                = AppCategory.Productive("development"),
            ["bash"]               = AppCategory.Productive("development"),
            ["zsh"]                = AppCategory.Productive("development"),
            ["git"]                = AppCategory.Productive("development"),
            ["github desktop"]     = AppCategory.Productive("development"),
            ["githubdesktop"]      = AppCategory.Productive("development"),
            ["sourcetree"]         = AppCategory.Productive("development"),
            ["gitkraken"]          = AppCategory.Productive("development"),
            ["fork"]               = AppCategory.Productive("development"),
            ["insomnia"]           = AppCategory.Productive("development"),
            ["postman"]            = AppCategory.Productive("development"),
            ["dbeaver"]            = AppCategory.Productive("development"),
            ["tableplus"]          = AppCategory.Productive("development"),
            ["datagrip"]           = AppCategory.Productive("development"),
            ["docker desktop"]     = AppCategory.Productive("development"),
            ["dockerdesktop"]      = AppCategory.Productive("development"),

            // ── Design ───────────────────────────────────────────────────────
            ["figma"]              = AppCategory.Productive("design"),
            ["sketch"]             = AppCategory.Productive("design"),
            ["xd"]                 = AppCategory.Productive("design"),       // Adobe XD
            ["photoshop"]          = AppCategory.Productive("design"),
            ["illustrator"]        = AppCategory.Productive("design"),
            ["affinity designer"]  = AppCategory.Productive("design"),
            ["affinity photo"]     = AppCategory.Productive("design"),
            ["canva"]              = AppCategory.Productive("design"),
            ["zeplin"]             = AppCategory.Productive("design"),
            ["invision"]           = AppCategory.Productive("design"),

            // ── Office / Productivity tools ──────────────────────────────────
            ["winword"]            = AppCategory.Productive("productivity_tools"),
            ["excel"]              = AppCategory.Productive("productivity_tools"),
            ["powerpnt"]           = AppCategory.Productive("productivity_tools"),
            ["onenote"]            = AppCategory.Productive("productivity_tools"),
            ["mspub"]              = AppCategory.Productive("productivity_tools"),
            ["notion"]             = AppCategory.Productive("productivity_tools"),
            ["obsidian"]           = AppCategory.Productive("productivity_tools"),
            ["evernote"]           = AppCategory.Productive("productivity_tools"),
            ["trello"]             = AppCategory.Productive("productivity_tools"),
            ["linear"]             = AppCategory.Productive("productivity_tools"),
            ["jira"]               = AppCategory.Productive("productivity_tools"),
            ["confluence"]         = AppCategory.Productive("productivity_tools"),
            ["asana"]              = AppCategory.Productive("productivity_tools"),
            ["clickup"]            = AppCategory.Productive("productivity_tools"),
            ["todoist"]            = AppCategory.Productive("productivity_tools"),

            // ── Communication (neutral — may be work) ────────────────────────
            ["slack"]              = AppCategory.Neutral("communication"),
            ["teams"]              = AppCategory.Neutral("communication"),
            ["discord"]            = AppCategory.Neutral("communication"),
            ["skype"]              = AppCategory.Neutral("communication"),
            ["telegram"]           = AppCategory.Neutral("communication"),
            ["whatsapp"]           = AppCategory.Neutral("communication"),
            ["outlook"]            = AppCategory.Neutral("communication"),
            ["thunderbird"]        = AppCategory.Neutral("communication"),
            ["mail"]               = AppCategory.Neutral("communication"),

            // ── Meetings ─────────────────────────────────────────────────────
            ["zoom"]               = AppCategory.Neutral("meetings"),
            ["loom"]               = AppCategory.Neutral("meetings"),
            ["whereby"]            = AppCategory.Neutral("meetings"),
            ["googlemeet"]         = AppCategory.Neutral("meetings"),

            // ── Browsers (neutral — can't tell what user is doing) ────────────
            ["chrome"]             = AppCategory.Neutral("browser_general"),
            ["firefox"]            = AppCategory.Neutral("browser_general"),
            ["msedge"]             = AppCategory.Neutral("browser_general"),
            ["opera"]              = AppCategory.Neutral("browser_general"),
            ["brave"]              = AppCategory.Neutral("browser_general"),
            ["safari"]             = AppCategory.Neutral("browser_general"),
            ["arc"]                = AppCategory.Neutral("browser_general"),
            ["iexplore"]           = AppCategory.Neutral("browser_general"),

            // ── Entertainment / Distraction ──────────────────────────────────
            ["spotify"]            = AppCategory.Distraction("entertainment"),
            ["vlc"]                = AppCategory.Distraction("entertainment"),
            ["mpc-hc64"]           = AppCategory.Distraction("entertainment"),
            ["mpv"]                = AppCategory.Distraction("entertainment"),
            ["steam"]              = AppCategory.Distraction("entertainment"),
            ["epicgameslauncher"]  = AppCategory.Distraction("entertainment"),
            ["riotclientservices"] = AppCategory.Distraction("entertainment"),
            ["leagueclient"]       = AppCategory.Distraction("entertainment"),
            ["netflix"]            = AppCategory.Distraction("entertainment"),
            ["twitchlauncher"]     = AppCategory.Distraction("entertainment"),
        };

    /// <summary>
    /// Classifies an app given its exe path and display name.
    /// Returns <see cref="AppCategory.Unknown"/> for unrecognised apps.
    /// </summary>
    public static AppCategory Classify(string? exePath, string? displayName)
    {
        // 1. Exact match by exe name (no extension, case-insensitive)
        if (!string.IsNullOrWhiteSpace(exePath))
        {
            var exeName = Path.GetFileNameWithoutExtension(exePath);
            if (ByExeName.TryGetValue(exeName, out var byExe))
                return byExe;
        }

        // 2. Fallback: display name contains a known key
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            var nameLower = displayName.ToLowerInvariant();
            foreach (var kvp in ByExeName)
            {
                if (nameLower.Contains(kvp.Key.ToLowerInvariant()))
                    return kvp.Value;
            }
        }

        return AppCategory.Unknown;
    }
}
