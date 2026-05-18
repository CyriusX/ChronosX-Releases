using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TimeTrack.Api.Extensions;
using TimeTrack.Api.Middleware;
using TimeTrack.Api.Security;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Evidence.Commands;
using TimeTrack.Backend.Application.Evidence.DTOs;
using TimeTrack.Backend.Application.Evidence.Queries;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Route("api/v1/evidence")]
[Authorize]
[RequireSubscription]
[EnableRateLimiting(RateLimitingExtensions.PolicyNames.Default)]
public sealed class EvidenceController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ICurrentUserContext _currentUser;

    public EvidenceController(ISender mediator, ICurrentUserContext currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Requests a presigned upload URL for a screenshot/evidence file
    /// </summary>
    [HttpPost("upload-url")]
    [ProducesResponseType(typeof(PresignedUploadUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PresignedUploadUrlResponse>> RequestUploadUrl(
        [FromBody] RequestPresignedUploadUrlRequest request,
        CancellationToken ct)
    {
        var command = new RequestPresignedUploadUrlCommand(request);
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Confirms that an evidence file has been uploaded to the presigned URL
    /// </summary>
    [HttpPost("{id:guid}/confirm-upload")]
    [ProducesResponseType(typeof(EvidenceItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<EvidenceItemResponse>> ConfirmUpload(
        [FromRoute] Guid id,
        [FromBody] ConfirmUploadRequest request,
        CancellationToken ct)
    {
        var command = new ConfirmUploadCommand(id, request.FileSizeBytes);
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Gets a presigned download URL for an evidence item
    /// </summary>
    [HttpGet("{id:guid}/download-url")]
    [ProducesResponseType(typeof(PresignedDownloadUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PresignedDownloadUrlResponse>> RequestDownloadUrl(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var command = new RequestPresignedDownloadUrlCommand(id);
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Gets presigned download URLs for multiple evidence items in batch
    /// </summary>
    [HttpPost("batch-download-urls")]
    [ProducesResponseType(typeof(IReadOnlyList<BatchDownloadUrlItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<BatchDownloadUrlItem>>> GetBatchDownloadUrls(
        [FromBody] List<Guid> evidenceIds,
        CancellationToken ct)
    {
        var query = new GetBatchDownloadUrlsQuery(evidenceIds);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Lists evidence items by period with pagination
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedEvidenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedEvidenceResponse>> GetEvidence(
        [FromQuery] Guid? userId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var startUtc = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);
        var query = new GetEvidenceByPeriodQuery(
            userId, startUtc, endUtc, type, page, pageSize);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Soft-deletes an evidence item (Colaborador: own only, Admin: any)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(DeleteEvidenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeleteEvidenceResponse>> DeleteEvidence(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var command = new DeleteEvidenceCommand(id);
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }
}
