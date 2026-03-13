using System.ComponentModel.DataAnnotations;
using MediatR;

namespace TimeTrack.Backend.Application.Reports.DTOs;

/// <summary>
/// Query para exportar dados de atividade em formato CSV
/// </summary>
public sealed record ExportCsvQuery(
    [Required]
    DateTime StartDate,

    [Required]
    DateTime EndDate,

    Guid? UserId = null,

    string Format = "csv"
) : IRequest<IAsyncEnumerable<ExportCsvRow>>;

/// <summary>
/// Response do export CSV (para metadados, se necessário)
/// </summary>
public sealed class ExportCsvResponse
{
    public string FileName { get; init; } = string.Empty;
    public int TotalRows { get; init; }
}

/// <summary>
/// DTO para exportar dados de atividade em formato CSV
/// </summary>
public sealed record ExportCsvRow(
    DateTime Data,
    string AppDisplayName,
    long TempoTotalSegundos,
    int SessoesCount
);
