using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Persistence.Seeds;

/// <summary>
/// Seed data for global app categories
///
/// SRP: Apenas popula a tabela de categorias globais
/// OCP: Novos apps podem ser adicionados sem modificar código existente
///
/// CX-143: Seed com ~80 apps/sites mais comuns no ambiente corporativo brasileiro
/// </summary>
public static class AppCategoryGlobalSeed
{
    /// <summary>
    /// Seed the app_category_global table with default data
    /// </summary>
    public static async Task SeedAsync(TimeTrackDbContext context, CancellationToken cancellationToken = default)
    {
        // Check if already seeded
        if (await context.AppCategoryGlobals.AnyAsync(cancellationToken))
        {
            return;
        }

        var categories = GetAllCategories();
        await context.AppCategoryGlobals.AddRangeAsync(categories, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Get all default categories
    /// </summary>
    public static IReadOnlyList<AppCategoryGlobal> GetAllCategories()
    {
        var categories = new List<AppCategoryGlobal>();

        // ========================================
        // PRODUCTIVE - Development
        // ========================================
        categories.AddRange(new[]
        {
            Create("code.exe", AppIdentifierType.Exe, "VS Code", AppProductivityCategory.Productive, AppSubcategory.Development),
            Create("vscode.exe", AppIdentifierType.Exe, "VS Code", AppProductivityCategory.Productive, AppSubcategory.Development),
            Create("devenv.exe", AppIdentifierType.Exe, "Visual Studio", AppProductivityCategory.Productive, AppSubcategory.Development),
            Create("idea64.exe", AppIdentifierType.Exe, "IntelliJ IDEA", AppProductivityCategory.Productive, AppSubcategory.Development),
            Create("idea.exe", AppIdentifierType.Exe, "IntelliJ IDEA", AppProductivityCategory.Productive, AppSubcategory.Development),
            Create("rider64.exe", AppIdentifierType.Exe, "JetBrains Rider", AppProductivityCategory.Productive, AppSubcategory.Development),
            Create("webstorm64.exe", AppIdentifierType.Exe, "WebStorm", AppProductivityCategory.Productive, AppSubcategory.Development),
            Create("pycharm64.exe", AppIdentifierType.Exe, "PyCharm", AppProductivityCategory.Productive, AppSubcategory.Development),
            Create("sublime_text.exe", AppIdentifierType.Exe, "Sublime Text", AppProductivityCategory.Productive, AppSubcategory.Development),
            Create("notepad++.exe", AppIdentifierType.Exe, "Notepad++", AppProductivityCategory.Productive, AppSubcategory.Development),
            Create("cursor.exe", AppIdentifierType.Exe, "Cursor", AppProductivityCategory.Productive, AppSubcategory.Development),
            Create("androidstudio64.exe", AppIdentifierType.Exe, "Android Studio", AppProductivityCategory.Productive, AppSubcategory.Development),
            Create("xcode.exe", AppIdentifierType.Exe, "Xcode", AppProductivityCategory.Productive, AppSubcategory.Development),
            Create("vim.exe", AppIdentifierType.Exe, "Vim", AppProductivityCategory.Productive, AppSubcategory.Development),
            Create("nvim.exe", AppIdentifierType.Exe, "Neovim", AppProductivityCategory.Productive, AppSubcategory.Development),
        });

        // ========================================
        // PRODUCTIVE - Design
        // ========================================
        categories.AddRange(new[]
        {
            Create("figma.exe", AppIdentifierType.Exe, "Figma", AppProductivityCategory.Productive, AppSubcategory.Design),
            Create("photoshop.exe", AppIdentifierType.Exe, "Adobe Photoshop", AppProductivityCategory.Productive, AppSubcategory.Design),
            Create("illustrator.exe", AppIdentifierType.Exe, "Adobe Illustrator", AppProductivityCategory.Productive, AppSubcategory.Design),
            Create("xd.exe", AppIdentifierType.Exe, "Adobe XD", AppProductivityCategory.Productive, AppSubcategory.Design),
            Create("aftereffects.exe", AppIdentifierType.Exe, "Adobe After Effects", AppProductivityCategory.Productive, AppSubcategory.Design),
            Create("sketch.exe", AppIdentifierType.Exe, "Sketch", AppProductivityCategory.Productive, AppSubcategory.Design),
            Create("canva.exe", AppIdentifierType.Exe, "Canva", AppProductivityCategory.Productive, AppSubcategory.Design),
        });

        // ========================================
        // PRODUCTIVE - Productivity Tools
        // ========================================
        categories.AddRange(new[]
        {
            Create("excel.exe", AppIdentifierType.Exe, "Excel", AppProductivityCategory.Productive, AppSubcategory.ProductivityTools),
            Create("winword.exe", AppIdentifierType.Exe, "Word", AppProductivityCategory.Productive, AppSubcategory.ProductivityTools),
            Create("powerpnt.exe", AppIdentifierType.Exe, "PowerPoint", AppProductivityCategory.Productive, AppSubcategory.ProductivityTools),
            Create("onenote.exe", AppIdentifierType.Exe, "OneNote", AppProductivityCategory.Productive, AppSubcategory.ProductivityTools),
            Create("msaccess.exe", AppIdentifierType.Exe, "Access", AppProductivityCategory.Productive, AppSubcategory.ProductivityTools),
            Create("notion.exe", AppIdentifierType.Exe, "Notion", AppProductivityCategory.Productive, AppSubcategory.ProductivityTools),
            Create("obsidian.exe", AppIdentifierType.Exe, "Obsidian", AppProductivityCategory.Productive, AppSubcategory.ProductivityTools),
        });

        // ========================================
        // PRODUCTIVE - Communication (work)
        // ========================================
        categories.AddRange(new[]
        {
            Create("outlook.exe", AppIdentifierType.Exe, "Outlook", AppProductivityCategory.Productive, AppSubcategory.Communication),
            Create("slack.exe", AppIdentifierType.Exe, "Slack", AppProductivityCategory.Productive, AppSubcategory.Communication),
            Create("teams.exe", AppIdentifierType.Exe, "Microsoft Teams", AppProductivityCategory.Productive, AppSubcategory.Communication),
            Create("lync.exe", AppIdentifierType.Exe, "Skype for Business", AppProductivityCategory.Productive, AppSubcategory.Communication),
            Create("skype.exe", AppIdentifierType.Exe, "Skype", AppProductivityCategory.Productive, AppSubcategory.Communication),
        });

        // ========================================
        // PRODUCTIVE - Meetings
        // ========================================
        categories.AddRange(new[]
        {
            Create("zoom.exe", AppIdentifierType.Exe, "Zoom", AppProductivityCategory.Productive, AppSubcategory.Meetings),
            Create("meet.exe", AppIdentifierType.Exe, "Google Meet", AppProductivityCategory.Productive, AppSubcategory.Meetings),
        });

        // ========================================
        // PRODUCTIVE - DevOps
        // ========================================
        categories.AddRange(new[]
        {
            Create("postman.exe", AppIdentifierType.Exe, "Postman", AppProductivityCategory.Productive, AppSubcategory.DevOps),
            Create("docker desktop.exe", AppIdentifierType.Exe, "Docker Desktop", AppProductivityCategory.Productive, AppSubcategory.DevOps),
            Create("insomnia.exe", AppIdentifierType.Exe, "Insomnia", AppProductivityCategory.Productive, AppSubcategory.DevOps),
            Create("git.exe", AppIdentifierType.Exe, "Git", AppProductivityCategory.Productive, AppSubcategory.DevOps),
            Create("sourcetree.exe", AppIdentifierType.Exe, "SourceTree", AppProductivityCategory.Productive, AppSubcategory.DevOps),
        });

        // ========================================
        // PRODUCTIVE - Domains
        // ========================================
        categories.AddRange(new[]
        {
            Create("github.com", AppIdentifierType.Domain, "GitHub", AppProductivityCategory.Productive, AppSubcategory.Development),
            Create("gitlab.com", AppIdentifierType.Domain, "GitLab", AppProductivityCategory.Productive, AppSubcategory.Development),
            Create("bitbucket.org", AppIdentifierType.Domain, "Bitbucket", AppProductivityCategory.Productive, AppSubcategory.Development),
            Create("linear.app", AppIdentifierType.Domain, "Linear", AppProductivityCategory.Productive, AppSubcategory.ProductivityTools),
            Create("notion.so", AppIdentifierType.Domain, "Notion", AppProductivityCategory.Productive, AppSubcategory.ProductivityTools),
            Create("atlassian.net", AppIdentifierType.Domain, "Jira/Confluence", AppProductivityCategory.Productive, AppSubcategory.ProductivityTools),
            Create("jira.atlassian.com", AppIdentifierType.Domain, "Jira", AppProductivityCategory.Productive, AppSubcategory.ProductivityTools),
            Create("confluence.atlassian.com", AppIdentifierType.Domain, "Confluence", AppProductivityCategory.Productive, AppSubcategory.Documentation),
            Create("figma.com", AppIdentifierType.Domain, "Figma", AppProductivityCategory.Productive, AppSubcategory.Design),
            Create("stackoverflow.com", AppIdentifierType.Domain, "Stack Overflow", AppProductivityCategory.Productive, AppSubcategory.Documentation),
            Create("docs.google.com", AppIdentifierType.Domain, "Google Docs", AppProductivityCategory.Productive, AppSubcategory.ProductivityTools),
            Create("sheets.google.com", AppIdentifierType.Domain, "Google Sheets", AppProductivityCategory.Productive, AppSubcategory.ProductivityTools),
            Create("slides.google.com", AppIdentifierType.Domain, "Google Slides", AppProductivityCategory.Productive, AppSubcategory.ProductivityTools),
            Create("meet.google.com", AppIdentifierType.Domain, "Google Meet", AppProductivityCategory.Productive, AppSubcategory.Meetings),
            Create("calendar.google.com", AppIdentifierType.Domain, "Google Calendar", AppProductivityCategory.Productive, AppSubcategory.ProductivityTools),
            Create("outlook.live.com", AppIdentifierType.Domain, "Outlook Web", AppProductivityCategory.Productive, AppSubcategory.Communication),
            Create("outlook.office.com", AppIdentifierType.Domain, "Outlook Web", AppProductivityCategory.Productive, AppSubcategory.Communication),
            Create("vercel.com", AppIdentifierType.Domain, "Vercel", AppProductivityCategory.Productive, AppSubcategory.DevOps),
            Create("netlify.com", AppIdentifierType.Domain, "Netlify", AppProductivityCategory.Productive, AppSubcategory.DevOps),
            Create("aws.amazon.com", AppIdentifierType.Domain, "AWS Console", AppProductivityCategory.Productive, AppSubcategory.DevOps),
            Create("console.cloud.google.com", AppIdentifierType.Domain, "Google Cloud", AppProductivityCategory.Productive, AppSubcategory.DevOps),
            Create("portal.azure.com", AppIdentifierType.Domain, "Azure Portal", AppProductivityCategory.Productive, AppSubcategory.DevOps),
        });

        // ========================================
        // NEUTRAL - Communication (personal)
        // ========================================
        categories.AddRange(new[]
        {
            Create("whatsapp.exe", AppIdentifierType.Exe, "WhatsApp Desktop", AppProductivityCategory.Neutral, AppSubcategory.Communication),
            Create("telegram.exe", AppIdentifierType.Exe, "Telegram Desktop", AppProductivityCategory.Neutral, AppSubcategory.Communication),
            Create("discord.exe", AppIdentifierType.Exe, "Discord", AppProductivityCategory.Neutral, AppSubcategory.Communication),
            Create("signal.exe", AppIdentifierType.Exe, "Signal", AppProductivityCategory.Neutral, AppSubcategory.Communication),
            Create("messenger.exe", AppIdentifierType.Exe, "Messenger", AppProductivityCategory.Neutral, AppSubcategory.Communication),
        });

        // ========================================
        // NEUTRAL - Browsers
        // ========================================
        categories.AddRange(new[]
        {
            Create("chrome.exe", AppIdentifierType.Exe, "Chrome", AppProductivityCategory.Neutral, AppSubcategory.BrowserGeneral),
            Create("firefox.exe", AppIdentifierType.Exe, "Firefox", AppProductivityCategory.Neutral, AppSubcategory.BrowserGeneral),
            Create("msedge.exe", AppIdentifierType.Exe, "Edge", AppProductivityCategory.Neutral, AppSubcategory.BrowserGeneral),
            Create("brave.exe", AppIdentifierType.Exe, "Brave", AppProductivityCategory.Neutral, AppSubcategory.BrowserGeneral),
            Create("opera.exe", AppIdentifierType.Exe, "Opera", AppProductivityCategory.Neutral, AppSubcategory.BrowserGeneral),
            Create("safari.exe", AppIdentifierType.Exe, "Safari", AppProductivityCategory.Neutral, AppSubcategory.BrowserGeneral),
        });

        // ========================================
        // NEUTRAL - System
        // ========================================
        categories.AddRange(new[]
        {
            Create("explorer.exe", AppIdentifierType.Exe, "Windows Explorer", AppProductivityCategory.Neutral, AppSubcategory.System),
            Create("cmd.exe", AppIdentifierType.Exe, "Command Prompt", AppProductivityCategory.Neutral, AppSubcategory.System),
            Create("powershell.exe", AppIdentifierType.Exe, "PowerShell", AppProductivityCategory.Neutral, AppSubcategory.System),
            Create("windowsterminal.exe", AppIdentifierType.Exe, "Windows Terminal", AppProductivityCategory.Neutral, AppSubcategory.System),
            Create("taskmgr.exe", AppIdentifierType.Exe, "Task Manager", AppProductivityCategory.Neutral, AppSubcategory.System),
            Create("systemsettings.exe", AppIdentifierType.Exe, "Windows Settings", AppProductivityCategory.Neutral, AppSubcategory.System),
            Create("searchapp.exe", AppIdentifierType.Exe, "Windows Search", AppProductivityCategory.Neutral, AppSubcategory.System),
            Create("searchui.exe", AppIdentifierType.Exe, "Windows Search", AppProductivityCategory.Neutral, AppSubcategory.System),
        });

        // ========================================
        // NEUTRAL - Domains
        // ========================================
        categories.AddRange(new[]
        {
            Create("gmail.com", AppIdentifierType.Domain, "Gmail", AppProductivityCategory.Neutral, AppSubcategory.Communication),
            Create("google.com", AppIdentifierType.Domain, "Google", AppProductivityCategory.Neutral, AppSubcategory.BrowserGeneral),
            Create("bing.com", AppIdentifierType.Domain, "Bing", AppProductivityCategory.Neutral, AppSubcategory.BrowserGeneral),
            Create("duckduckgo.com", AppIdentifierType.Domain, "DuckDuckGo", AppProductivityCategory.Neutral, AppSubcategory.BrowserGeneral),
            Create("wikipedia.org", AppIdentifierType.Domain, "Wikipedia", AppProductivityCategory.Neutral, AppSubcategory.BrowserGeneral),
        });

        // ========================================
        // DISTRACTION - Social Media
        // ========================================
        categories.AddRange(new[]
        {
            Create("instagram.com", AppIdentifierType.Domain, "Instagram", AppProductivityCategory.Distraction, AppSubcategory.SocialMedia),
            Create("facebook.com", AppIdentifierType.Domain, "Facebook", AppProductivityCategory.Distraction, AppSubcategory.SocialMedia),
            Create("twitter.com", AppIdentifierType.Domain, "Twitter/X", AppProductivityCategory.Distraction, AppSubcategory.SocialMedia),
            Create("x.com", AppIdentifierType.Domain, "Twitter/X", AppProductivityCategory.Distraction, AppSubcategory.SocialMedia),
            Create("tiktok.com", AppIdentifierType.Domain, "TikTok", AppProductivityCategory.Distraction, AppSubcategory.SocialMedia),
            Create("linkedin.com", AppIdentifierType.Domain, "LinkedIn", AppProductivityCategory.Distraction, AppSubcategory.SocialMedia),
            Create("reddit.com", AppIdentifierType.Domain, "Reddit", AppProductivityCategory.Distraction, AppSubcategory.SocialMedia),
            Create("pinterest.com", AppIdentifierType.Domain, "Pinterest", AppProductivityCategory.Distraction, AppSubcategory.SocialMedia),
            Create("threads.net", AppIdentifierType.Domain, "Threads", AppProductivityCategory.Distraction, AppSubcategory.SocialMedia),
        });

        // ========================================
        // DISTRACTION - Entertainment
        // ========================================
        categories.AddRange(new[]
        {
            Create("youtube.com", AppIdentifierType.Domain, "YouTube", AppProductivityCategory.Distraction, AppSubcategory.Entertainment),
            Create("youtu.be", AppIdentifierType.Domain, "YouTube", AppProductivityCategory.Distraction, AppSubcategory.Entertainment),
            Create("netflix.com", AppIdentifierType.Domain, "Netflix", AppProductivityCategory.Distraction, AppSubcategory.Entertainment),
            Create("primevideo.com", AppIdentifierType.Domain, "Amazon Prime Video", AppProductivityCategory.Distraction, AppSubcategory.Entertainment),
            Create("disneyplus.com", AppIdentifierType.Domain, "Disney+", AppProductivityCategory.Distraction, AppSubcategory.Entertainment),
            Create("hbomax.com", AppIdentifierType.Domain, "HBO Max", AppProductivityCategory.Distraction, AppSubcategory.Entertainment),
            Create("twitch.tv", AppIdentifierType.Domain, "Twitch", AppProductivityCategory.Distraction, AppSubcategory.Entertainment),
            Create("vimeo.com", AppIdentifierType.Domain, "Vimeo", AppProductivityCategory.Distraction, AppSubcategory.Entertainment),
            Create("spotify.exe", AppIdentifierType.Exe, "Spotify", AppProductivityCategory.Distraction, AppSubcategory.MusicStreaming),
            Create("spotify.com", AppIdentifierType.Domain, "Spotify Web", AppProductivityCategory.Distraction, AppSubcategory.MusicStreaming),
            Create("music.apple.com", AppIdentifierType.Domain, "Apple Music", AppProductivityCategory.Distraction, AppSubcategory.MusicStreaming),
        });

        // ========================================
        // DISTRACTION - Gaming
        // ========================================
        categories.AddRange(new[]
        {
            Create("steam.exe", AppIdentifierType.Exe, "Steam", AppProductivityCategory.Distraction, AppSubcategory.Gaming),
            Create("epicgameslauncher.exe", AppIdentifierType.Exe, "Epic Games", AppProductivityCategory.Distraction, AppSubcategory.Gaming),
            Create("origin.exe", AppIdentifierType.Exe, "EA Origin", AppProductivityCategory.Distraction, AppSubcategory.Gaming),
            Create("battlenet.exe", AppIdentifierType.Exe, "Battle.net", AppProductivityCategory.Distraction, AppSubcategory.Gaming),
            Create("gog galaxy.exe", AppIdentifierType.Exe, "GOG Galaxy", AppProductivityCategory.Distraction, AppSubcategory.Gaming),
            Create("riotclient.exe", AppIdentifierType.Exe, "Riot Client", AppProductivityCategory.Distraction, AppSubcategory.Gaming),
        });

        // ========================================
        // DISTRACTION - News
        // ========================================
        categories.AddRange(new[]
        {
            Create("g1.globo.com", AppIdentifierType.Domain, "G1", AppProductivityCategory.Distraction, AppSubcategory.News),
            Create("uol.com.br", AppIdentifierType.Domain, "UOL", AppProductivityCategory.Distraction, AppSubcategory.News),
            Create("folha.uol.com.br", AppIdentifierType.Domain, "Folha de S.Paulo", AppProductivityCategory.Distraction, AppSubcategory.News),
            Create("estadao.com.br", AppIdentifierType.Domain, "Estadão", AppProductivityCategory.Distraction, AppSubcategory.News),
            Create("bbc.com", AppIdentifierType.Domain, "BBC", AppProductivityCategory.Distraction, AppSubcategory.News),
            Create("cnn.com", AppIdentifierType.Domain, "CNN", AppProductivityCategory.Distraction, AppSubcategory.News),
        });

        // ========================================
        // DISTRACTION - Shopping
        // ========================================
        categories.AddRange(new[]
        {
            Create("amazon.com.br", AppIdentifierType.Domain, "Amazon", AppProductivityCategory.Distraction, AppSubcategory.Shopping),
            Create("mercadolivre.com.br", AppIdentifierType.Domain, "Mercado Livre", AppProductivityCategory.Distraction, AppSubcategory.Shopping),
            Create("magazineluiza.com.br", AppIdentifierType.Domain, "Magazine Luiza", AppProductivityCategory.Distraction, AppSubcategory.Shopping),
            Create("americanas.com.br", AppIdentifierType.Domain, "Americanas", AppProductivityCategory.Distraction, AppSubcategory.Shopping),
            Create("aliexpress.com", AppIdentifierType.Domain, "AliExpress", AppProductivityCategory.Distraction, AppSubcategory.Shopping),
            Create("shopee.com.br", AppIdentifierType.Domain, "Shopee", AppProductivityCategory.Distraction, AppSubcategory.Shopping),
        });

        return categories;
    }

    private static AppCategoryGlobal Create(
        string identifier,
        AppIdentifierType identifierType,
        string displayName,
        AppProductivityCategory productivity,
        AppSubcategory subcategory)
    {
        return AppCategoryGlobal.Create(identifier, identifierType, displayName, productivity, subcategory);
    }
}
