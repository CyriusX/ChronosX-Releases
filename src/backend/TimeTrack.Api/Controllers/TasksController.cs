using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TimeTrack.Api.Extensions;
using TimeTrack.Backend.Application.Tasks.Commands;
using TimeTrack.Backend.Application.Tasks.DTOs;
using TimeTrack.Backend.Application.Tasks.Queries;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting(RateLimitingExtensions.PolicyNames.Default)]
public sealed class TasksController : ControllerBase
{
    private readonly IMediator _mediator;

    public TasksController(IMediator mediator) => _mediator = mediator;

    // ─────────────────────────────────────────────────────────────────────
    // Project-scoped task list / create
    // ─────────────────────────────────────────────────────────────────────

    [HttpGet("api/v1/projects/{projectId:guid}/tasks")]
    [ProducesResponseType(typeof(ListTasksResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ListTasksResponse>> ListProjectTasks(Guid projectId)
    {
        var result = await _mediator.Send(new ListProjectTasksQuery(projectId));
        return Ok(result);
    }

    [HttpPost("api/v1/projects/{projectId:guid}/tasks")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> CreateTask(Guid projectId, [FromBody] CreateTaskRequest request)
    {
        var result = await _mediator.Send(new CreateTaskCommand(
            projectId,
            request.Title,
            request.Description,
            request.AssignedUserId,
            request.Priority,
            request.DueDate));
        return CreatedAtAction(nameof(GetTask), new { id = result.Id }, result);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Single task ops
    // ─────────────────────────────────────────────────────────────────────

    [HttpGet("api/v1/tasks/{id:guid}")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> GetTask(Guid id)
    {
        var result = await _mediator.Send(new GetTaskByIdQuery(id));
        return Ok(result);
    }

    [HttpPut("api/v1/tasks/{id:guid}")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> UpdateTask(Guid id, [FromBody] UpdateTaskRequest request)
    {
        var result = await _mediator.Send(new UpdateTaskCommand(
            id,
            request.Title,
            request.Description,
            request.AssignedUserId,
            request.Priority,
            request.DueDate));
        return Ok(result);
    }

    [HttpPatch("api/v1/tasks/{id:guid}/move")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskResponse>> MoveTask(Guid id, [FromBody] MoveTaskRequest request)
    {
        var result = await _mediator.Send(new MoveTaskCommand(id, request.Status, request.Position, request.RowVersion));
        return Ok(result);
    }

    [HttpDelete("api/v1/tasks/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTask(Guid id)
    {
        await _mediator.Send(new DeleteTaskCommand(id));
        return NoContent();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Admin/Manager: list tasks for a specific user or the whole team
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lists tasks for a specific user. Admin/Gestor only.
    /// Pass no userId (or "all") to list tasks for the entire org.
    /// </summary>
    [HttpGet("api/v1/users/tasks")]
    [ProducesResponseType(typeof(ListTasksResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ListTasksResponse>> ListUserTasks(
        [FromQuery] Guid? userId = null,
        [FromQuery] bool includeDone = false)
    {
        var result = await _mediator.Send(new ListUserTasksQuery(userId, includeDone));
        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Self (current user) task endpoints
    // ─────────────────────────────────────────────────────────────────────

    [HttpGet("api/v1/me/tasks")]
    [ProducesResponseType(typeof(ListTasksResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ListTasksResponse>> ListMyTasks([FromQuery] bool includeDone = false)
    {
        var result = await _mediator.Send(new ListMyTasksQuery(includeDone));
        return Ok(result);
    }

    [HttpGet("api/v1/me/tasks/open")]
    [ProducesResponseType(typeof(OpenTaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetMyOpenTask()
    {
        var result = await _mediator.Send(new GetMyOpenTaskQuery());
        if (result is null) return NoContent();
        return Ok(result);
    }

    [HttpPost("api/v1/me/tasks/open/pause")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PauseMyOpenTask()
    {
        await _mediator.Send(new PauseOpenTaskTimerCommand());
        return NoContent();
    }

    [HttpPost("api/v1/me/tasks/open/resume")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResumeMyOpenTask()
    {
        await _mediator.Send(new ResumeOpenTaskTimerCommand());
        return NoContent();
    }

    [HttpPost("api/v1/me/tasks/open/close-on-idle-reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CloseOpenTaskOnIdleReject()
    {
        await _mediator.Send(new CloseOpenTaskTimerOnIdleRejectCommand());
        return NoContent();
    }

    [HttpGet("api/v1/me/task-entries")]
    [ProducesResponseType(typeof(ListTaskEntriesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ListTaskEntriesResponse>> GetMyTaskEntries([FromQuery] string? date = null)
    {
        var dateOnly = date is not null && DateOnly.TryParse(date, out var parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await _mediator.Send(new ListMyTaskEntriesQuery(dateOnly));
        return Ok(result);
    }
}
