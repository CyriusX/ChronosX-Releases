using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MediatR;

namespace TimeTrack.Backend.Application.Reports.DTOs;

/// <summary>
/// Query for top accessed folders (derived from ActivitySessions.FilePath).
/// </summary>
public sealed record TopFoldersQuery(
    [Required] Guid? UserId,
    [Required] DateTime StartDate,
    [Required] DateTime EndDate,
    int Limit = 20,
    string? Timezone = null,
    IReadOnlyList<Guid>? UserIds = null
) : IRequest<TopFoldersResponse>;

public sealed class TopFoldersResponse
{
    [JsonPropertyName("folders")]
    public List<TopFolderItem> Folders { get; init; } = [];
}

public sealed class TopFolderItem
{
    [JsonPropertyName("folderPath")]
    public string FolderPath { get; init; } = string.Empty;

    [JsonPropertyName("totalSeconds")]
    public long TotalSeconds { get; init; }

    [JsonPropertyName("visitCount")]
    public int VisitCount { get; init; }
}

