using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TimeTrack.Api.Extensions;
using TimeTrack.Backend.Application.ProjectMembers.Commands;
using TimeTrack.Backend.Application.ProjectMembers.DTOs;
using TimeTrack.Backend.Application.ProjectMembers.Queries;
using TimeTrack.Backend.Application.Projects.Commands;
using TimeTrack.Backend.Application.Projects.DTOs;
using TimeTrack.Backend.Application.Projects.Queries;

namespace TimeTrack.Api.Controllers;

/// <summary>
/// Controller de projetos
/// </summary>
[ApiController]
[Route("api/v1/projects")]
[Authorize]
[EnableRateLimiting(RateLimitingExtensions.PolicyNames.Default)]
public sealed class ProjectsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProjectsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lista todos os projetos da organização
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ListProjectsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ListProjectsResponse>> ListProjects(
        [FromQuery] bool? activeOnly = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        var result = await _mediator.Send(new ListProjectsQuery(activeOnly, page, pageSize));
        return Ok(result);
    }

    /// <summary>
    /// Obtém um projeto específico
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> GetProject(Guid id)
    {
        var result = await _mediator.Send(new GetProjectQuery(id));
        return Ok(result);
    }

    /// <summary>
    /// Cria um novo projeto
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectResponse>> CreateProject([FromBody] CreateProjectRequest request)
    {
        var result = await _mediator.Send(new CreateProjectCommand(
            request.Name,
            request.Description,
            request.Color,
            request.IsBillable,
            request.Currency,
            request.HourlyRate));

        return CreatedAtAction(nameof(GetProject), new { id = result.Id }, result);
    }

    /// <summary>
    /// Atualiza um projeto existente
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> UpdateProject(Guid id, [FromBody] UpdateProjectRequest request)
    {
        var result = await _mediator.Send(new UpdateProjectCommand(
            id,
            request.Name,
            request.Description,
            request.Color,
            request.IsBillable,
            request.Currency,
            request.HourlyRate));

        return Ok(result);
    }

    /// <summary>
    /// Arquiva um projeto (soft delete)
    /// </summary>
    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ArchiveProject(Guid id)
    {
        await _mediator.Send(new ArchiveProjectCommand(id));
        return NoContent();
    }

    /// <summary>
    /// Reativa um projeto arquivado
    /// </summary>
    [HttpPost("{id:guid}/reactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReactivateProject(Guid id)
    {
        await _mediator.Send(new ReactivateProjectCommand(id));
        return NoContent();
    }

    /// <summary>
    /// Exclui permanentemente um projeto
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProject(Guid id)
    {
        await _mediator.Send(new DeleteProjectCommand(id));
        return NoContent();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Project Members
    // ─────────────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(typeof(ListProjectMembersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ListProjectMembersResponse>> ListMembers(Guid id)
    {
        var result = await _mediator.Send(new ListProjectMembersQuery(id));
        return Ok(result);
    }

    [HttpPost("{id:guid}/members")]
    [ProducesResponseType(typeof(ProjectMemberResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectMemberResponse>> AddMember(Guid id, [FromBody] AddProjectMemberRequest request)
    {
        var result = await _mediator.Send(new AddProjectMemberCommand(id, request.UserId, request.Role));
        return CreatedAtAction(nameof(ListMembers), new { id }, result);
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
    {
        await _mediator.Send(new RemoveProjectMemberCommand(id, userId));
        return NoContent();
    }
}
