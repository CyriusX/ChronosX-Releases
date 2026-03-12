using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeTrack.Backend.Application.Auth.Commands;
using TimeTrack.Backend.Application.Auth.DTOs;
using TimeTrack.Backend.Application.Common.Interfaces;

namespace TimeTrack.Api.Controllers;

/// <summary>
/// Controller de autenticação
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserContext _currentUserContext;

    public AuthController(IMediator mediator, ICurrentUserContext currentUserContext)
    {
        _mediator = mediator;
        _currentUserContext = currentUserContext;
    }

    /// <summary>
    /// Login com email e senha
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await _mediator.Send(new LoginCommand(request.Email, request.Password));
        return Ok(result);
    }

    /// <summary>
    /// Registro de nova organização e usuário admin (B2C)
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
    {
        var result = await _mediator.Send(new RegisterCommand(
            request.Email,
            request.Password,
            request.DisplayName,
            request.OrganizationName));
        return Ok(result);
    }

    /// <summary>
    /// Refresh do token de acesso
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RefreshTokenResponse>> Refresh([FromBody] RefreshTokenRequest request)
    {
        var result = await _mediator.Send(new RefreshTokenCommand(request.RefreshToken));
        return Ok(result);
    }

    /// <summary>
    /// Logout (revoga refresh tokens)
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request = null)
    {
        await _mediator.Send(new LogoutCommand(request?.RefreshToken));
        return NoContent();
    }

    /// <summary>
    /// Solicita reset de senha
    /// </summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ForgotPasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await _mediator.Send(new ForgotPasswordCommand(request.Email));
        return Ok(result);
    }

    /// <summary>
    /// Reseta a senha usando token
    /// </summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ResetPasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResetPasswordResponse>> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _mediator.Send(new ResetPasswordCommand(request.Token, request.NewPassword));
        return Ok(result);
    }

    /// <summary>
    /// Altera a senha do usuário logado
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ChangePasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ChangePasswordResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = _currentUserContext.UserId
            ?? throw new UnauthorizedAccessException("User not authenticated");

        var result = await _mediator.Send(new ChangePasswordCommand(
            userId,
            request.CurrentPassword,
            request.NewPassword));
        return Ok(result);
    }

    /// <summary>
    /// Lista membros da organização
    /// </summary>
    [HttpGet("members")]
    [Authorize]
    [ProducesResponseType(typeof(ListMembersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ListMembersResponse>> ListMembers()
    {
        var orgId = _currentUserContext.OrgId
            ?? throw new UnauthorizedAccessException("User not associated with an organization");

        var result = await _mediator.Send(new ListMembersCommand(orgId));
        return Ok(result);
    }

    /// <summary>
    /// Convida um novo usuário para a organização (B2B)
    /// </summary>
    [HttpPost("invite")]
    [Authorize(Roles = "Admin,Gestor")]
    [ProducesResponseType(typeof(InviteUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<InviteUserResponse>> InviteUser([FromBody] InviteUserRequest request)
    {
        var orgId = _currentUserContext.OrgId
            ?? throw new UnauthorizedAccessException("User not associated with an organization");

        var result = await _mediator.Send(new InviteUserCommand(
            orgId,
            request.Email,
            request.DisplayName,
            request.Role));
        return Ok(result);
    }

    /// <summary>
    /// Atualiza status de um membro da organização
    /// </summary>
    [HttpPut("members/status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMemberStatus([FromBody] UpdateMemberStatusRequest request)
    {
        var orgId = _currentUserContext.OrgId
            ?? throw new UnauthorizedAccessException("User not associated with an organization");

        await _mediator.Send(new UpdateMemberStatusCommand(orgId, request.UserId, request.Status));
        return NoContent();
    }

    /// <summary>
    /// Atualiza role de um membro da organização
    /// </summary>
    [HttpPut("members/role")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMemberRole([FromBody] UpdateMemberRoleRequest request)
    {
        var orgId = _currentUserContext.OrgId
            ?? throw new UnauthorizedAccessException("User not associated with an organization");

        await _mediator.Send(new UpdateMemberRoleCommand(orgId, request.UserId, request.Role));
        return NoContent();
    }

    /// <summary>
    /// Obtém status da equipe com tempo trabalhado hoje
    /// </summary>
    [HttpGet("team/status")]
    [Authorize(Roles = "Admin,Gestor")]
    [ProducesResponseType(typeof(TeamStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TeamStatusResponse>> GetTeamStatus()
    {
        var orgId = _currentUserContext.OrgId
            ?? throw new UnauthorizedAccessException("User not associated with an organization");

        var result = await _mediator.Send(new GetTeamStatusCommand(orgId));
        return Ok(result);
    }
}
