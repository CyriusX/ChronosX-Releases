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
}
