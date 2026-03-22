/**
 * Export Reports to CSV
 *
 * SRP: Apenas converte dados de relatório para formato CSV e faz download
 * OCP: Extensível para novos tipos de export (PDF, XLSX, etc.)
 */

import type {
  TopAppsResponse,
  TopPathsResponse,
  CategoryDistributionResponse,
  DistractionStatsResponse,
  DailySummaryRangeResponse,
  ProductivityTrendResponse,
} from '../types/reports';

// ============================================================================
// TYPES
// ============================================================================

export interface ExportData {
  dailySummaryRange: DailySummaryRangeResponse | null;
  productivityTrend: ProductivityTrendResponse | null;
  topApps: TopAppsResponse | null;
  topPaths: TopPathsResponse | null;
  distractionStats: DistractionStatsResponse | null;
  categoryDistribution: CategoryDistributionResponse | null;
}

export interface ExportOptions {
  /** Period label for filename */
  periodLabel: string;
  /** Date range */
  startDate: string;
  endDate: string;
  /** User name being exported */
  userName?: string;
}

// ============================================================================
// CSV HELPERS
// ============================================================================

/**
 * Format seconds to readable duration (Xh Ym)
 */
function formatDurationReadable(seconds: number): string {
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  if (hours > 0) {
    return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`;
  }
  return `${minutes}m`;
}

/**
 * Download a string as a file
 */
function downloadFile(content: string, filename: string, mimeType: string): void {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

/**
 * Create a section header with visual separator
 */
function sectionHeader(title: string): string {
  const separator = '═'.repeat(60);
  return `\n${separator}\n${title}\n${separator}\n`;
}

/**
 * Create a subsection header
 */
function subHeader(title: string): string {
  const line = '─'.repeat(40);
  return `\n${line}\n${title}\n${line}\n`;
}

// ============================================================================
// EXPORT FUNCTIONS
// ============================================================================

/**
 * Export header with report metadata
 */
function exportHeader(options: ExportOptions): string {
  const now = new Date();
  const generatedAt = now.toLocaleString('pt-BR');

  return `╔════════════════════════════════════════════════════════════╗
║              RELATÓRIO DE PRODUTIVIDADE                    ║
╚════════════════════════════════════════════════════════════╝

INFORMAÇÕES GERAIS
─────────────────────────────────────────────────────────────
Período:            ${options.periodLabel}
Data Início:        ${options.startDate}
Data Fim:           ${options.endDate}
Usuário:            ${options.userName ?? 'Meus dados'}
Gerado em:          ${generatedAt}
`;
}

/**
 * Export summary statistics
 */
function exportSummary(data: ExportData): string {
  const days = data.dailySummaryRange?.days ?? [];
  const totalActiveSeconds = days.reduce((sum, d) => sum + d.totalActiveSeconds, 0);
  const totalIdleSeconds = days.reduce((sum, d) => sum + d.totalIdleSeconds, 0);
  const totalTime = totalActiveSeconds + totalIdleSeconds;
  const avgProductivity = days.length > 0
    ? days.reduce((sum, d) => sum + d.productivityRatio, 0) / days.length
    : 0;
  const daysWithData = days.filter((d) => d.totalActiveSeconds > 0).length;

  const activePercent = totalTime > 0 ? ((totalActiveSeconds / totalTime) * 100).toFixed(1) : '0';
  const idlePercent = totalTime > 0 ? ((totalIdleSeconds / totalTime) * 100).toFixed(1) : '0';

  return sectionHeader('RESUMO EXECUTIVO') +
`Métrica,Valor,Percentual
Tempo Total Registrado,${formatDurationReadable(totalTime)},100%
Tempo Ativo,${formatDurationReadable(totalActiveSeconds)},${activePercent}%
Tempo Idle,${formatDurationReadable(totalIdleSeconds)},${idlePercent}%
Produtividade Média,${Math.round(avgProductivity * 100)}%,-
Dias com Dados,${daysWithData} dias,-
`;
}

/**
 * Export activity heatmap data
 */
function exportActivityHeatmap(data: ExportData): string {
  const days = data.dailySummaryRange?.days ?? [];
  if (days.length === 0) {
    return sectionHeader('MAPA DE ATIVIDADE') + '\nNenhum dado disponível para o período.\n';
  }

  const rows = days.map((d) => {
    const total = d.totalActiveSeconds + d.totalIdleSeconds;
    return `${d.date},${formatDurationReadable(d.totalActiveSeconds)},${formatDurationReadable(d.totalIdleSeconds)},${Math.round(d.productivityRatio * 100)}%,${formatDurationReadable(total)}`;
  });

  return sectionHeader('MAPA DE ATIVIDADE (Detalhado por Dia)') +
`Data,Tempo Ativo,Tempo Idle,Produtividade,Tempo Total
${rows.join('\n')}
`;
}

/**
 * Export productivity trend
 */
function exportProductivityTrend(data: ExportData): string {
  const periods = data.productivityTrend?.periods ?? [];
  if (periods.length === 0) {
    return sectionHeader('TENDÊNCIA DE PRODUTIVIDADE') + '\nNenhum dado disponível para o período.\n';
  }

  const rows = periods.map((p) => {
    const total = p.productiveSeconds + p.neutralSeconds + p.distractionSeconds + p.idleSeconds;
    const productivePercent = total > 0 ? ((p.productiveSeconds / total) * 100).toFixed(1) : '0';
    return `${p.period},${formatDurationReadable(p.productiveSeconds)},${formatDurationReadable(p.neutralSeconds)},${formatDurationReadable(p.distractionSeconds)},${formatDurationReadable(p.idleSeconds)},${productivePercent}%`;
  });

  return sectionHeader('TENDÊNCIA DE PRODUTIVIDADE') +
`Período,Produtivo,Neutro,Distração,Idle,% Produtivo
${rows.join('\n')}
`;
}

/**
 * Export top apps
 */
function exportTopApps(data: ExportData): string {
  const apps = data.topApps?.apps ?? [];
  if (apps.length === 0) {
    return sectionHeader('APLICATIVOS MAIS UTILIZADOS') + '\nNenhum dado disponível para o período.\n';
  }

  const rows = apps.map((app, idx) => {
    const category = app.productivity ?? 'Não classificado';
    const subcat = app.subcategory ?? '-';
    const percent = app.percentage?.toFixed(1) ?? '-';
    return `${idx + 1},${app.displayName},${formatDurationReadable(app.totalSeconds)},${app.sessionCount},${category},${subcat},${percent}%`;
  });

  return sectionHeader('APLICATIVOS MAIS UTILIZADOS') +
`#,Aplicativo,Tempo Total,Sessões,Categoria,Subcategoria,% do Total
${rows.join('\n')}
`;
}

/**
 * Export top paths
 */
function exportTopPaths(data: ExportData): string {
  const paths = data.topPaths?.paths ?? [];
  if (paths.length === 0) {
    return sectionHeader('URLs E CAMINHOS MAIS ACESSADOS') + '\nNenhum dado disponível para o período.\n';
  }

  const rows = paths.map((p, idx) => {
    const cleanPath = p.filePath ?? p.path ?? '-';
    return `${idx + 1},"${p.title}","${cleanPath}",${p.sourceApp},${formatDurationReadable(p.totalSeconds)},${p.visitCount}`;
  });

  return sectionHeader('URLs E CAMINHOS MAIS ACESSADOS') +
`#,Título,Caminho/URL,Aplicativo,Tempo Total,Visitas
${rows.join('\n')}
`;
}

/**
 * Export category distribution
 */
function exportCategoryDistribution(data: ExportData): string {
  const categories = data.categoryDistribution?.categories ?? [];
  if (categories.length === 0) {
    return sectionHeader('DISTRIBUIÇÃO POR CATEGORIA') + '\nNenhum dado disponível para o período.\n';
  }

  let csv = sectionHeader('DISTRIBUIÇÃO POR CATEGORIA');
  csv += 'Categoria,Tempo Total,% Total,Subcategoria,Tempo Subcat.,% Subcat.\n';

  categories.forEach((cat) => {
    if (cat.subcategories.length === 0) {
      csv += `${cat.category},${formatDurationReadable(cat.totalSeconds)},${cat.percentage.toFixed(1)}%,-,-,-\n`;
    } else {
      cat.subcategories.forEach((sub, idx) => {
        if (idx === 0) {
          csv += `${cat.category},${formatDurationReadable(cat.totalSeconds)},${cat.percentage.toFixed(1)}%,${sub.name},${formatDurationReadable(sub.totalSeconds)},${sub.percentage.toFixed(1)}%\n`;
        } else {
          csv += `,-,-,${sub.name},${formatDurationReadable(sub.totalSeconds)},${sub.percentage.toFixed(1)}%\n`;
        }
      });
    }
  });

  return csv + '\n';
}

/**
 * Export distraction stats
 */
function exportDistractionStats(data: ExportData): string {
  const stats = data.distractionStats;
  if (!stats) {
    return sectionHeader('ANÁLISE DE DISTRAÇÕES') + '\nNenhum dado disponível para o período.\n';
  }

  let csv = sectionHeader('ANÁLISE DE DISTRAÇÕES');

  // Top distractions
  if (stats.topDistractions.length > 0) {
    csv += subHeader('Principais Fontes de Distração');
    csv += '#,Aplicativo,Tempo Total,Sessões,Subcategoria\n';

    stats.topDistractions.forEach((d, idx) => {
      csv += `${idx + 1},${d.displayName},${formatDurationReadable(d.totalSeconds)},${d.sessionCount},${d.subcategory ?? '-'}\n`;
    });
  }

  // Daily distraction breakdown
  if (stats.dailyDistractions.length > 0) {
    csv += subHeader('Distrações por Dia');
    csv += 'Data,Tempo em Distração\n';

    stats.dailyDistractions.forEach((d) => {
      csv += `${d.date},${formatDurationReadable(d.distractionSeconds)}\n`;
    });
  }

  return csv + '\n';
}

/**
 * Export footer
 */
function exportFooter(): string {
  return `
╔════════════════════════════════════════════════════════════╗
║                    FIM DO RELATÓRIO                        ║
╚════════════════════════════════════════════════════════════╝

Relatório gerado automaticamente pelo XChronus.
`;
}

// ============================================================================
// MAIN EXPORT FUNCTION
// ============================================================================

/**
 * Export all report data to a single CSV file
 */
export function exportReportsToCSV(data: ExportData, options: ExportOptions): void {
  const timestamp = new Date().toISOString().split('T')[0];
  const filename = `relatorio-produtividade-${timestamp}.csv`;

  let csv = '';
  csv += exportHeader(options);
  csv += exportSummary(data);
  csv += exportActivityHeatmap(data);
  csv += exportProductivityTrend(data);
  csv += exportTopApps(data);
  csv += exportTopPaths(data);
  csv += exportCategoryDistribution(data);
  csv += exportDistractionStats(data);
  csv += exportFooter();

  // Add BOM for Excel UTF-8 compatibility
  const bom = '\uFEFF';
  downloadFile(bom + csv, filename, 'text/csv;charset=utf-8');
}
