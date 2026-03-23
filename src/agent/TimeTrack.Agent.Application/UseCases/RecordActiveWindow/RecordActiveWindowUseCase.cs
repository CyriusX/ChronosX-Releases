using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.Services;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Application.UseCases.RecordActiveWindow;

/// <summary>
/// Use Case para registrar a janela ativa atual
/// Ponto de entrada principal do loop de captura do Agent
/// </summary>
public sealed class RecordActiveWindowUseCase
{
    private readonly IActivitySessionRepository _sessionRepository;
    private readonly ITrackingStateRepository _stateRepository;
    private readonly ICurrentUserContext _userContext;
    private readonly IIdempotencyKeyGenerator _idempotencyKeyGenerator;
    private readonly ILogger<RecordActiveWindowUseCase> _logger;

    private readonly int _pollingIntervalSeconds;

    /// <summary>
    /// Intervalo padrão entre capturas (em segundos)
    /// </summary>
    private const int DefaultCaptureIntervalSeconds = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RecordActiveWindowUseCase(
        IActivitySessionRepository sessionRepository,
        ITrackingStateRepository stateRepository,
        ICurrentUserContext userContext,
        IIdempotencyKeyGenerator idempotencyKeyGenerator,
        ILogger<RecordActiveWindowUseCase> logger,
        int pollingIntervalSeconds = 5)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        _idempotencyKeyGenerator = idempotencyKeyGenerator ?? throw new ArgumentNullException(nameof(idempotencyKeyGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pollingIntervalSeconds = pollingIntervalSeconds;
        if (_pollingIntervalSeconds <= 0)
            _pollingIntervalSeconds = 5;
    }

    /// <summary>
    /// Executa o registro da janela ativa
    /// </summary>
    public async Task<RecordActiveWindowResponse> ExecuteAsync(
        RecordActiveWindowRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ExecutablePath))
            throw new ArgumentException("ExecutablePath is required", nameof(request));

        if (string.IsNullOrWhiteSpace(request.ApplicationName))
            throw new ArgumentException("ApplicationName is required", nameof(request));

        var userId = _userContext.UserId
            ?? throw new InvalidOperationException("User not authenticated");

        // Verifica se o tracking está ativo
        var state = await _stateRepository.GetAsync(userId, cancellationToken);
        if (state != null && !state.IsActive)
        {
            _logger.LogDebug("Tracking is paused, skipping window capture");
            return new RecordActiveWindowResponse
            {
                SessionId = Guid.Empty,
                ApplicationName = request.ApplicationName,
                IsNewSession = false,
                SessionDuration = TimeSpan.Zero
            };
        }

        // Extract site name from browser window title (e.g., "Telegram Web", "YouTube", "GitHub")
        var siteName = request.BrowserUrl; // BrowserUrl carries the extracted site name from title parsing

        // Cria identidade da aplicação (uses site name for browser categorization when available)
        var appIdentity = CreateAppIdentity(request, siteName);

        // Cria hash da janela para agrupamento
        var windowHash = ComputeWindowHash(request.WindowTitle);

        // Busca sessão ativa para possível extensão (filtrada por usuário)
        var activeSession = await _sessionRepository.GetActiveSessionAsync(userId, cancellationToken);

        ActivitySession session;
        bool isNewSession;

        if (activeSession != null && CanExtendSession(activeSession, appIdentity, windowHash))
        {
            // Estende a sessão existente - não cria novo outbox item
            activeSession.Extend(request.CapturedAt);
            await _sessionRepository.UpdateAsync(activeSession, cancellationToken);
            session = activeSession;
            isNewSession = false;

            _logger.LogDebug(
                "Extended session for {App} ({Duration:mm\\:ss})",
                appIdentity.DisplayName, session.Duration);
        }
        else
        {
            // Cria nova sessão (com userId e domínio)
            var period = new TimeRange(
                request.CapturedAt,
                request.CapturedAt.AddSeconds(DefaultCaptureIntervalSeconds));

            session = ActivitySession.Create(userId, appIdentity, period, windowHash, request.WindowTitle, request.FilePath, siteName);
            await SaveSessionWithOutboxAsync(session, cancellationToken);
            isNewSession = true;

            _logger.LogDebug(
                "Created new session for {App} (site: {Site})",
                appIdentity.DisplayName, siteName ?? "N/A");
        }

        return new RecordActiveWindowResponse
        {
            SessionId = session.Id,
            ApplicationName = appIdentity.DisplayName,
            IsNewSession = isNewSession,
            SessionDuration = session.Duration
        };
    }

    // Known browser executable names (without extension, lowercase)
    private static readonly HashSet<string> BrowserExeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "firefox", "msedge", "opera", "brave", "safari", "arc", "iexplore",
        "vivaldi", "waterfox", "chromium", "librewolf"
    };

    /// <summary>
    /// Checks if the executable path belongs to a browser
    /// </summary>
    private static bool IsBrowser(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return false;
        var exeName = Path.GetFileNameWithoutExtension(exePath);
        return BrowserExeNames.Contains(exeName);
    }

    /// <summary>
    /// Extracts the tab/site title from a browser window title.
    /// Browser titles typically follow the pattern: "Tab Title - Browser Name"
    /// </summary>
    private static string ExtractBrowserTabTitle(string? windowTitle, string browserDisplayName)
    {
        if (string.IsNullOrWhiteSpace(windowTitle))
            return browserDisplayName;

        // Strip common browser suffixes: " - Google Chrome", " - Mozilla Firefox", " — Mozilla Firefox", etc.
        var separators = new[] { " - ", " — ", " – " };
        foreach (var sep in separators)
        {
            var lastIdx = windowTitle.LastIndexOf(sep, StringComparison.Ordinal);
            if (lastIdx > 0)
            {
                var tabTitle = windowTitle[..lastIdx].Trim();
                if (tabTitle.Length > 0)
                    return tabTitle;
            }
        }

        return windowTitle;
    }

    /// <summary>
    /// Cria a identidade da aplicação a partir do request.
    /// For browsers, uses the browser product name (e.g. "Google Chrome") as DisplayName
    /// and classifies productivity by the extracted site name (priority) or full tab title (fallback).
    /// Tab/site details are tracked separately via WindowTitle/Domain on the session.
    /// </summary>
    private static AppIdentity CreateAppIdentity(RecordActiveWindowRequest request, string? siteName)
    {
        var exePathHash = ComputeHash(request.ExecutablePath);

        if (IsBrowser(request.ExecutablePath))
        {
            // Classify by extracted site name first (e.g. "Telegram Web", "YouTube"),
            // fall back to full tab title if no site name was extracted.
            var classifyTarget = !string.IsNullOrWhiteSpace(siteName)
                ? siteName
                : ExtractBrowserTabTitle(request.WindowTitle, request.ApplicationName);

            var tabCategory = BrowserTabCategorizer.Classify(classifyTarget);

            // DisplayName = "Browser - Site" (e.g. "Google Chrome - Telegram Web")
            // Falls back to just the browser name if no site was extracted.
            var displayName = !string.IsNullOrWhiteSpace(siteName)
                ? $"{request.ApplicationName} - {siteName}"
                : request.ApplicationName;

            return new AppIdentity(exePathHash, displayName, tabCategory);
        }

        var appCategory = AppCategorizer.Classify(request.ExecutablePath, request.ApplicationName);
        return new AppIdentity(exePathHash, request.ApplicationName, appCategory);
    }

    /// <summary>
    /// Verifica se pode estender a sessão existente.
    /// Sessions extend when the executable, display name AND window hash all match.
    /// For browsers: DisplayName is the browser name (e.g. "Google Chrome") so it always
    /// matches for the same browser. The WindowHash detects tab/site changes — switching
    /// from youtube.com to github.com creates a new session under the same browser name.
    /// For regular apps: DisplayName and WindowHash both stay constant while focused.
    /// </summary>
    private static bool CanExtendSession(
        ActivitySession session,
        AppIdentity newApp,
        string? newWindowHash)
    {
        if (session.App.ExePathHash != newApp.ExePathHash)
            return false;

        if (session.App.DisplayName != newApp.DisplayName)
            return false;

        // Compare window hash to detect tab/page changes within the same app.
        // If both are non-null and differ, a new session should be created.
        if (session.WindowHash != null && newWindowHash != null)
            return session.WindowHash == newWindowHash;

        return true;
    }

    /// <summary>
    /// Computa hash SHA256 truncado para o caminho do executável
    /// </summary>
    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    /// <summary>
    /// Computa hash para o título da janela (para agrupamento)
    /// </summary>
    private static string? ComputeWindowHash(string? windowTitle)
    {
        if (string.IsNullOrWhiteSpace(windowTitle))
            return null;

        return ComputeHash(windowTitle);
    }

    /// <summary>
    /// Salva a sessão com outbox item em uma única transação
    /// </summary>
    private async Task SaveSessionWithOutboxAsync(ActivitySession session, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "SaveSessionWithOutboxAsync: SessionId={SessionId}, App={App}",
            session.Id,
            session.App.DisplayName);

        var payload = CreateSessionPayload(session);
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var idempotencyKey = _idempotencyKeyGenerator.Generate(
            "activity_session",
            session.Id,
            session.Period.StartUtc);

        var outboxItem = OutboxItem.Create(
            "activity_session",
            session.Id,
            payloadJson,
            idempotencyKey);

        _logger.LogInformation(
            "Created outbox item: Id={OutboxId}, EntityType={EntityType}, EntityId={EntityId}",
            outboxItem.Id,
            outboxItem.EntityType,
            outboxItem.EntityId);

        await _sessionRepository.SaveWithOutboxAsync(session, new[] { outboxItem }, cancellationToken);
    }

    /// <summary>
    /// Cria o payload para sincronização
    /// </summary>
    private static SessionSyncPayload CreateSessionPayload(ActivitySession session)
    {
        return new SessionSyncPayload
        {
            Id = session.Id,
            ExePathHash = session.App.ExePathHash,
            DisplayName = session.App.DisplayName,
            CategoryProductivity = session.App.Category.Productivity,
            CategorySubcategory = session.App.Category.Subcategory,
            CategorySource = session.App.Category.Source,
            StartUtc = session.Period.StartUtc,
            EndUtc = session.Period.EndUtc,
            WindowHash = session.WindowHash,
            WindowTitle = session.WindowTitle,
            FilePath = session.FilePath,
            Domain = session.Domain
        };
    }

    /// <summary>
    /// Payload de sincronização para activity_session
    /// </summary>
    private sealed class SessionSyncPayload
    {
        public Guid Id { get; init; }
        public string ExePathHash { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string CategoryProductivity { get; init; } = string.Empty;
        public string CategorySubcategory { get; init; } = string.Empty;
        public string CategorySource { get; init; } = string.Empty;
        public DateTime StartUtc { get; init; }
        public DateTime EndUtc { get; init; }
        public string? WindowHash { get; init; }
        public string? WindowTitle { get; init; }
        public string? FilePath { get; init; }
        public string? Domain { get; init; }
    }
}