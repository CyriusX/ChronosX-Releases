using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Application.UseCases.RecordActiveWindow;

/// <summary>
/// Classifies browser tabs by their title into productivity categories.
/// Matches known site/service names in the tab title.
/// </summary>
internal static class BrowserTabCategorizer
{
    private static readonly (string Pattern, AppCategory Category)[] Rules =
    {
        // Development
        ("github",           AppCategory.Productive("development")),
        ("gitlab",           AppCategory.Productive("development")),
        ("bitbucket",        AppCategory.Productive("development")),
        ("stackoverflow",    AppCategory.Productive("development")),
        ("stack overflow",   AppCategory.Productive("development")),
        ("dev.to",           AppCategory.Productive("development")),
        ("codepen",          AppCategory.Productive("development")),
        ("codesandbox",      AppCategory.Productive("development")),
        ("jsfiddle",         AppCategory.Productive("development")),
        ("npm",              AppCategory.Productive("development")),
        ("docker hub",       AppCategory.Productive("development")),
        ("azure devops",     AppCategory.Productive("development")),
        ("vercel",           AppCategory.Productive("development")),
        ("netlify",          AppCategory.Productive("development")),
        ("heroku",           AppCategory.Productive("development")),
        ("aws console",      AppCategory.Productive("development")),
        ("localhost",        AppCategory.Productive("development")),

        // Productivity tools
        ("notion",           AppCategory.Productive("productivity_tools")),
        ("linear",           AppCategory.Productive("productivity_tools")),
        ("jira",             AppCategory.Productive("productivity_tools")),
        ("confluence",       AppCategory.Productive("productivity_tools")),
        ("trello",           AppCategory.Productive("productivity_tools")),
        ("asana",            AppCategory.Productive("productivity_tools")),
        ("clickup",          AppCategory.Productive("productivity_tools")),
        ("monday.com",       AppCategory.Productive("productivity_tools")),
        ("google docs",      AppCategory.Productive("productivity_tools")),
        ("google sheets",    AppCategory.Productive("productivity_tools")),
        ("google slides",    AppCategory.Productive("productivity_tools")),
        ("google drive",     AppCategory.Productive("productivity_tools")),
        ("dropbox",          AppCategory.Productive("productivity_tools")),
        ("onedrive",         AppCategory.Productive("productivity_tools")),
        ("sharepoint",       AppCategory.Productive("productivity_tools")),

        // Communication
        ("gmail",            AppCategory.Neutral("communication")),
        ("outlook",          AppCategory.Neutral("communication")),
        ("slack",            AppCategory.Neutral("communication")),
        ("discord",          AppCategory.Neutral("communication")),
        ("teams",            AppCategory.Neutral("communication")),
        ("whatsapp",         AppCategory.Neutral("communication")),
        ("telegram",         AppCategory.Neutral("communication")),
        ("messenger",        AppCategory.Neutral("communication")),

        // Meetings
        ("google meet",      AppCategory.Neutral("meetings")),
        ("zoom",             AppCategory.Neutral("meetings")),

        // Research (neutral)
        ("google search",    AppCategory.Neutral("browser_general")),
        ("google",           AppCategory.Neutral("browser_general")),
        ("bing",             AppCategory.Neutral("browser_general")),
        ("wikipedia",        AppCategory.Neutral("browser_general")),

        // Entertainment / Distraction
        ("youtube",          AppCategory.Distraction("entertainment")),
        ("netflix",          AppCategory.Distraction("entertainment")),
        ("twitch",           AppCategory.Distraction("entertainment")),
        ("disney+",          AppCategory.Distraction("entertainment")),
        ("prime video",      AppCategory.Distraction("entertainment")),
        ("hbo max",          AppCategory.Distraction("entertainment")),
        ("spotify",          AppCategory.Distraction("entertainment")),

        // Social media
        ("reddit",           AppCategory.Distraction("social_media")),
        ("twitter",          AppCategory.Distraction("social_media")),
        ("x.com",            AppCategory.Distraction("social_media")),
        ("facebook",         AppCategory.Distraction("social_media")),
        ("instagram",        AppCategory.Distraction("social_media")),
        ("tiktok",           AppCategory.Distraction("social_media")),
        ("linkedin",         AppCategory.Neutral("social_media")),
    };

    public static AppCategory Classify(string tabTitle)
    {
        if (string.IsNullOrWhiteSpace(tabTitle))
            return AppCategory.Neutral("browser_general");

        var lower = tabTitle.ToLowerInvariant();

        foreach (var (pattern, category) in Rules)
        {
            if (lower.Contains(pattern))
                return category;
        }

        return AppCategory.Neutral("browser_general");
    }

    // ================================================================
    // Domain-based classification (higher accuracy than tab title)
    // ================================================================

    private static readonly (string DomainPattern, AppCategory Category)[] DomainRules =
    {
        // Development
        ("github.com",          AppCategory.Productive("development")),
        ("gitlab.com",          AppCategory.Productive("development")),
        ("bitbucket.org",       AppCategory.Productive("development")),
        ("stackoverflow.com",   AppCategory.Productive("development")),
        ("stackexchange.com",   AppCategory.Productive("development")),
        ("dev.to",              AppCategory.Productive("development")),
        ("codepen.io",          AppCategory.Productive("development")),
        ("codesandbox.io",      AppCategory.Productive("development")),
        ("jsfiddle.net",        AppCategory.Productive("development")),
        ("npmjs.com",           AppCategory.Productive("development")),
        ("hub.docker.com",      AppCategory.Productive("development")),
        ("devops.azure.com",    AppCategory.Productive("development")),
        ("vercel.com",          AppCategory.Productive("development")),
        ("netlify.com",         AppCategory.Productive("development")),
        ("heroku.com",          AppCategory.Productive("development")),
        ("console.aws.amazon.com", AppCategory.Productive("development")),
        ("localhost",           AppCategory.Productive("development")),
        ("127.0.0.1",           AppCategory.Productive("development")),

        // Productivity tools
        ("notion.so",           AppCategory.Productive("productivity_tools")),
        ("linear.app",          AppCategory.Productive("productivity_tools")),
        ("atlassian.net",       AppCategory.Productive("productivity_tools")),  // Jira, Confluence
        ("trello.com",          AppCategory.Productive("productivity_tools")),
        ("asana.com",           AppCategory.Productive("productivity_tools")),
        ("clickup.com",         AppCategory.Productive("productivity_tools")),
        ("monday.com",          AppCategory.Productive("productivity_tools")),
        ("docs.google.com",     AppCategory.Productive("productivity_tools")),
        ("sheets.google.com",   AppCategory.Productive("productivity_tools")),
        ("slides.google.com",   AppCategory.Productive("productivity_tools")),
        ("drive.google.com",    AppCategory.Productive("productivity_tools")),
        ("dropbox.com",         AppCategory.Productive("productivity_tools")),
        ("onedrive.live.com",   AppCategory.Productive("productivity_tools")),
        ("sharepoint.com",      AppCategory.Productive("productivity_tools")),
        ("figma.com",           AppCategory.Productive("design")),
        ("canva.com",           AppCategory.Productive("design")),

        // Communication
        ("mail.google.com",     AppCategory.Neutral("communication")),
        ("outlook.live.com",    AppCategory.Neutral("communication")),
        ("outlook.office.com",  AppCategory.Neutral("communication")),
        ("slack.com",           AppCategory.Neutral("communication")),
        ("discord.com",         AppCategory.Neutral("communication")),
        ("teams.microsoft.com", AppCategory.Neutral("communication")),
        ("web.whatsapp.com",    AppCategory.Neutral("communication")),
        ("web.telegram.org",    AppCategory.Neutral("communication")),
        ("messenger.com",       AppCategory.Neutral("communication")),

        // Meetings
        ("meet.google.com",     AppCategory.Neutral("meetings")),
        ("zoom.us",             AppCategory.Neutral("meetings")),

        // Research (neutral)
        ("google.com",          AppCategory.Neutral("browser_general")),
        ("bing.com",            AppCategory.Neutral("browser_general")),
        ("wikipedia.org",       AppCategory.Neutral("browser_general")),
        ("duckduckgo.com",      AppCategory.Neutral("browser_general")),

        // Entertainment / Distraction
        ("youtube.com",         AppCategory.Distraction("entertainment")),
        ("netflix.com",         AppCategory.Distraction("entertainment")),
        ("twitch.tv",           AppCategory.Distraction("entertainment")),
        ("disneyplus.com",      AppCategory.Distraction("entertainment")),
        ("primevideo.com",      AppCategory.Distraction("entertainment")),
        ("max.com",             AppCategory.Distraction("entertainment")),
        ("open.spotify.com",    AppCategory.Distraction("entertainment")),
        ("music.youtube.com",   AppCategory.Distraction("entertainment")),

        // Social media
        ("reddit.com",          AppCategory.Distraction("social_media")),
        ("twitter.com",         AppCategory.Distraction("social_media")),
        ("x.com",               AppCategory.Distraction("social_media")),
        ("facebook.com",        AppCategory.Distraction("social_media")),
        ("instagram.com",       AppCategory.Distraction("social_media")),
        ("tiktok.com",          AppCategory.Distraction("social_media")),
        ("linkedin.com",        AppCategory.Neutral("social_media")),

        // Shopping
        ("amazon.com",          AppCategory.Distraction("shopping")),
        ("mercadolivre.com.br", AppCategory.Distraction("shopping")),
        ("shopee.com.br",       AppCategory.Distraction("shopping")),
    };

    /// <summary>
    /// Classifies a browser tab by its actual domain (extracted from URL).
    /// More accurate than tab-title matching since it uses the real website address.
    /// Falls back to tab-title-based classification if no domain rule matches.
    /// </summary>
    public static AppCategory ClassifyByDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return AppCategory.Neutral("browser_general");

        var lower = domain.ToLowerInvariant();

        // Exact and suffix matching against domain rules
        foreach (var (pattern, category) in DomainRules)
        {
            // "web.telegram.org" ends with "telegram.org", "mail.google.com" ends with "google.com"
            if (lower == pattern || lower.EndsWith("." + pattern, StringComparison.Ordinal))
                return category;
        }

        // No domain rule matched — return neutral as default for unknown sites
        return AppCategory.Neutral("browser_general");
    }
}
