using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Api.Security;
using TimeTrack.Backend.AI.Interfaces;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Route("api/v1/admin/ai/classifications")]
[Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
public sealed class AiClassificationsController : ControllerBase
{
    private readonly TimeTrackDbContext _context;
    private readonly IAIService _aiService;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<AiClassificationsController> _logger;

    public AiClassificationsController(
        TimeTrackDbContext context,
        IAIService aiService,
        ICurrentUserContext currentUser,
        ILogger<AiClassificationsController> logger)
    {
        _context = context;
        _aiService = aiService;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetPending(
        [FromQuery] string? confidence = null,
        [FromQuery] string? decisionType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var validTypes = new HashSet<string> { "app_classification", "app_usage_suggestion" };

        var query = _context.AiDecisionLogs
            .IgnoreQueryFilters()
            .Where(d => !d.WasReviewed);

        if (!string.IsNullOrEmpty(decisionType) && validTypes.Contains(decisionType))
        {
            query = query.Where(d => d.DecisionType == decisionType);
        }
        else
        {
            query = query.Where(d => validTypes.Contains(d.DecisionType));
        }

        query = query.OrderByDescending(d => d.CreatedAt);

        var total = await query.CountAsync(ct);

        var rawItems = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rawItems.Select(d =>
        {
            var exeName = d.InputData.ValueKind == JsonValueKind.Object
                ? d.InputData.TryGetProperty("exe_name", out var exeEl) ? exeEl.GetString() : null
                : null;
            var suggestedCategory = d.Output.ValueKind == JsonValueKind.Object
                ? d.Output.TryGetProperty("category", out var catEl) ? catEl.GetString() : null
                : null;
            var suggestedSubcategory = d.Output.ValueKind == JsonValueKind.Object
                ? d.Output.TryGetProperty("subcategory", out var sub) ? sub.GetString() : null
                : null;
            var confidenceVal = d.Output.ValueKind == JsonValueKind.Object
                ? d.Output.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : (double?)null
                : null;
            var reasoning = d.Output.ValueKind == JsonValueKind.Object
                ? d.Output.TryGetProperty("reasoning", out var reason) ? reason.GetString() : null
                : null;

            // Per-user data for usage suggestions
            List<object>? topUsers = null;
            double? totalOrgHours = null;
            List<string>? sampleWindowTitles = null;
            string? currentCategory = null;

            if (d.DecisionType == "app_usage_suggestion" && d.InputData.ValueKind == JsonValueKind.Object)
            {
                currentCategory = d.InputData.TryGetProperty("current_category", out var cc) ? cc.GetString() : null;
                totalOrgHours = d.InputData.TryGetProperty("total_org_hours", out var toh) ? toh.GetDouble() : (double?)null;

                if (d.InputData.TryGetProperty("sample_window_titles", out var wtEl) && wtEl.ValueKind == JsonValueKind.Array)
                {
                    sampleWindowTitles = wtEl.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
                }

                if (d.InputData.TryGetProperty("per_user_breakdown", out var pubEl) && pubEl.ValueKind == JsonValueKind.Array)
                {
                    topUsers = pubEl.EnumerateArray().Select(e => (object)new
                    {
                        userId = e.TryGetProperty("user_id", out var uid) ? uid.GetString() : null,
                        userName = e.TryGetProperty("user_name", out var un) ? un.GetString() : null,
                        hours = e.TryGetProperty("hours", out var h) ? h.GetDouble() : 0
                    }).ToList();
                }
            }

            return new
            {
                d.Id,
                DecisionType = d.DecisionType,
                ExeName = exeName,
                CurrentCategory = currentCategory,
                SuggestedCategory = suggestedCategory,
                SuggestedSubcategory = suggestedSubcategory,
                Confidence = confidenceVal,
                Reasoning = reasoning,
                TopUsers = topUsers,
                TotalOrgHours = totalOrgHours,
                SampleWindowTitles = sampleWindowTitles,
                d.CreatedAt
            };
        }).Where(i => !string.IsNullOrEmpty(i.ExeName) || !string.IsNullOrEmpty(i.SuggestedCategory)).ToList();

        return Ok(new { items, total, page, pageSize });
    }

    [HttpPost("{decisionId:guid}/review")]
    public async Task<IActionResult> Review(
        Guid decisionId,
        [FromBody] ReviewClassificationRequest request,
        CancellationToken ct = default)
    {
        if (_currentUser.OrgId == null) return Forbid();

        var decision = await _context.AiDecisionLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == decisionId, ct);

        if (decision == null)
            return NotFound("Decision not found");

        if (decision.WasReviewed)
            return BadRequest("Decision already reviewed");

        if (decision.DecisionType != "app_classification" && decision.DecisionType != "app_usage_suggestion")
            return BadRequest("Invalid decision type");

        var exeName = decision.InputData.GetProperty("exe_name").GetString() ?? "";

        switch (request.Outcome)
        {
            case "accepted":
                await AcceptClassificationAsync(decision, exeName, _currentUser.OrgId.Value, ct);
                break;

            case "corrected":
                if (string.IsNullOrEmpty(request.CorrectCategory))
                    return BadRequest("correct_category is required when outcome is 'corrected'");

                await CorrectClassificationAsync(decision, exeName, _currentUser.OrgId.Value, request, ct);
                break;

            case "rejected":
                decision.MarkReviewed("rejected");
                break;

            default:
                return BadRequest("Invalid outcome. Use: accepted, corrected, rejected");
        }

        await _context.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(exeName))
            _aiService.InvalidateClassificationCache(exeName);

        return Ok(new { decisionId, request.Outcome });
    }

    [HttpPost("accept-all")]
    public async Task<IActionResult> AcceptAll(CancellationToken ct = default)
    {
        if (_currentUser.OrgId == null) return Forbid();

        var highConfidence = await _context.AiDecisionLogs
            .IgnoreQueryFilters()
            .Where(d => !d.WasReviewed
                && (d.DecisionType == "app_classification" || d.DecisionType == "app_usage_suggestion"))
            .ToListAsync(ct);

        var accepted = 0;
        foreach (var decision in highConfidence)
        {
            var confidence = decision.Output.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0;
            if (confidence < 0.90) continue;

            var exeName = decision.InputData.GetProperty("exe_name").GetString() ?? "";
            var category = decision.Output.GetProperty("category").GetString() ?? "neutral";

            decision.MarkReviewed("accepted");

            var productivity = MapCategory(category);
            var overrideEntity = AppCategoryOverride.Create(
                identifier: exeName,
                identifierType: AppIdentifierType.Exe,
                productivity: productivity,
                subcategory: EnsureSubcategoryCompatibility(productivity, MapSubcategory(decision)),
                orgId: _currentUser.OrgId.Value,
                displayName: exeName,
                createdBy: _currentUser.UserId ?? Guid.Empty,
                note: "Auto-approved via AI classification review (confidence >= 0.90)");

            await _context.AppCategoryOverrides.AddAsync(overrideEntity, ct);
            accepted++;
        }

        await _context.SaveChangesAsync(ct);

        foreach (var decision in highConfidence.Where(d =>
        {
            var conf = d.Output.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0;
            return conf >= 0.90;
        }))
        {
            var exe = decision.InputData.GetProperty("exe_name").GetString() ?? "";
            if (!string.IsNullOrEmpty(exe))
                _aiService.InvalidateClassificationCache(exe);
        }

        return Ok(new { accepted });
    }

    private async Task AcceptClassificationAsync(
        AiDecisionLog decision,
        string exeName,
        Guid orgId,
        CancellationToken ct)
    {
        var category = decision.Output.GetProperty("category").GetString() ?? "neutral";

        decision.MarkReviewed("accepted");

        var productivity = MapCategory(category);
        var overrideEntity = AppCategoryOverride.Create(
            identifier: exeName,
            identifierType: AppIdentifierType.Exe,
            productivity: productivity,
            subcategory: EnsureSubcategoryCompatibility(productivity, MapSubcategory(decision)),
            orgId: orgId,
            displayName: exeName,
            createdBy: _currentUser.UserId ?? Guid.Empty,
            note: "Approved via AI classification review");

        await _context.AppCategoryOverrides.AddAsync(overrideEntity, ct);
    }

    private async Task CorrectClassificationAsync(
        AiDecisionLog decision,
        string exeName,
        Guid orgId,
        ReviewClassificationRequest request,
        CancellationToken ct)
    {
        var correctValue = JsonSerializer.SerializeToElement(new
        {
            category = request.CorrectCategory,
            subcategory = request.CorrectSubcategory
        });

        decision.MarkReviewed("corrected", correctValue);

        var category = MapCategory(request.CorrectCategory);
        var rawSubcategory = string.IsNullOrEmpty(request.CorrectSubcategory)
            ? AppSubcategory.Unknown
            : MapSubcategoryString(request.CorrectSubcategory);

        var overrideEntity = AppCategoryOverride.Create(
            identifier: exeName,
            identifierType: AppIdentifierType.Exe,
            productivity: category,
            subcategory: EnsureSubcategoryCompatibility(category, rawSubcategory),
            orgId: orgId,
            displayName: exeName,
            createdBy: _currentUser.UserId ?? Guid.Empty,
            note: "Corrected via AI classification review");

        await _context.AppCategoryOverrides.AddAsync(overrideEntity, ct);
    }

    private static AppProductivityCategory MapCategory(string category) => category.ToLowerInvariant() switch
    {
        "productive" => AppProductivityCategory.Productive,
        "distraction" => AppProductivityCategory.Distraction,
        _ => AppProductivityCategory.Neutral
    };

    private static AppSubcategory MapSubcategory(AiDecisionLog decision)
    {
        if (!decision.Output.TryGetProperty("subcategory", out var subEl)) return AppSubcategory.Unknown;
        var sub = subEl.GetString() ?? "";
        return MapSubcategoryString(sub);
    }

    private static AppSubcategory MapSubcategoryString(string sub) => sub.ToLowerInvariant() switch
    {
        "development" => AppSubcategory.Development,
        "design" => AppSubcategory.Design,
        "communication" => AppSubcategory.Communication,
        "productivity_tools" or "productivity" => AppSubcategory.ProductivityTools,
        "meetings" => AppSubcategory.Meetings,
        "documentation" => AppSubcategory.Documentation,
        "devops" => AppSubcategory.DevOps,
        "finance" => AppSubcategory.Finance,
        "social_media" => AppSubcategory.SocialMedia,
        "entertainment" => AppSubcategory.Entertainment,
        "gaming" => AppSubcategory.Gaming,
        "news" => AppSubcategory.News,
        "music_streaming" => AppSubcategory.MusicStreaming,
        "shopping" => AppSubcategory.Shopping,
        "browser_general" => AppSubcategory.BrowserGeneral,
        "system" => AppSubcategory.System,
        "file_manager" => AppSubcategory.FileManager,
        "utilities" => AppSubcategory.Utilities,
        _ => AppSubcategory.Unknown
    };

    private static AppSubcategory EnsureSubcategoryCompatibility(
        AppProductivityCategory productivity,
        AppSubcategory subcategory)
    {
        return productivity switch
        {
            AppProductivityCategory.Productive => subcategory switch
            {
                AppSubcategory.Development or
                AppSubcategory.Design or
                AppSubcategory.Communication or
                AppSubcategory.ProductivityTools or
                AppSubcategory.Productivity or
                AppSubcategory.Meetings or
                AppSubcategory.Documentation or
                AppSubcategory.DevOps or
                AppSubcategory.Finance => subcategory,
                _ => AppSubcategory.ProductivityTools
            },
            AppProductivityCategory.Neutral => subcategory switch
            {
                AppSubcategory.BrowserGeneral or
                AppSubcategory.System or
                AppSubcategory.Unknown or
                AppSubcategory.FileManager or
                AppSubcategory.Utilities or
                AppSubcategory.Communication => subcategory,
                _ => AppSubcategory.Unknown
            },
            AppProductivityCategory.Distraction => subcategory switch
            {
                AppSubcategory.SocialMedia or
                AppSubcategory.Entertainment or
                AppSubcategory.Gaming or
                AppSubcategory.News or
                AppSubcategory.MusicStreaming or
                AppSubcategory.Shopping => subcategory,
                _ => AppSubcategory.Entertainment
            },
            _ => AppSubcategory.Unknown
        };
    }
}

public sealed record ReviewClassificationRequest
{
    public string Outcome { get; init; } = string.Empty;
    public string? CorrectCategory { get; init; }
    public string? CorrectSubcategory { get; init; }
}
