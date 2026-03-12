using System;

namespace TimeTrack.Backend.Domain.DTOs;

/// <summary>
/// DTO para exportar dados de atividade em formato CSV
/// </summary>
public sealed record ExportCsvRow(
    DateTime Data,
    string AppDisplayName,
    long TempoTotalSegundos,
    int SessoesCount
);
