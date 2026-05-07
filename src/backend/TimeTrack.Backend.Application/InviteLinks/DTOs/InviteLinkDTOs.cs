using System.ComponentModel.DataAnnotations;

namespace TimeTrack.Backend.Application.InviteLinks.DTOs;

public sealed class GenerateInviteLinkResponse
{
    public Guid Id { get; init; }
    public string Token { get; init; } = string.Empty;
    public string LinkUrl { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class InviteLinkInfoResponse
{
    public string OrgName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public bool IsValid { get; init; }
}

public sealed class InviteLinkItem
{
    public Guid Id { get; init; }
    public string Role { get; init; } = string.Empty;
    public int UseCount { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class ListInviteLinksResponse
{
    public List<InviteLinkItem> Links { get; init; } = [];
}

public sealed class RegisterViaInviteLinkRequest
{
    [Required]
    public string DisplayName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; init; } = string.Empty;
}
