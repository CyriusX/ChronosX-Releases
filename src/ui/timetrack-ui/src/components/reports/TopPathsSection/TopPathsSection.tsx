/**
 * TopPathsSection - Top URLs and paths list
 *
 * SRP: Apenas exibe lista de URLs e caminhos mais acessados
 * OCP: Extensível via props
 * DIP: Recebe dados via props
 *
 * Composition: Composto por PathItem
 */

import { useState, useMemo } from 'react';
import { ExternalLink, FolderOpen, MoreHorizontal, FileText } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../../ui/card';
import type { TopPathItem } from '../../../types/reports';

export interface TopPathsSectionProps {
  /** Top paths data */
  paths: TopPathItem[];
  /** Loading state */
  isLoading?: boolean;
  /** Title */
  title?: string;
  /** Max items to show */
  maxItems?: number;
}

function formatTime(seconds: number): string {
  if (seconds === 0) return '0m';
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  if (hours > 0) {
    return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`;
  }
  return `${minutes}m`;
}

function isUrl(path: string): boolean {
  return path.startsWith('http://') || path.startsWith('https://');
}

function PathItem({
  title,
  filePath,
  path,
  sourceApp,
  totalSeconds,
  visitCount,
  maxSeconds,
}: TopPathItem & { maxSeconds: number }) {
  const [isExpanded, setIsExpanded] = useState(false);
  const isWebUrl = isUrl(path);
  const percentage = (totalSeconds / maxSeconds) * 100;

  const handleToggle = () => {
    setIsExpanded((prev) => !prev);
  };

  return (
    <div
      className={`group py-2 px-3 rounded-lg transition-colors cursor-pointer ${
        isExpanded
          ? 'bg-[rgba(74,217,255,0.08)] border border-[rgba(74,217,255,0.2)]'
          : 'hover:bg-[rgba(255,255,255,0.02)] border border-transparent'
      }`}
      onClick={handleToggle}
    >
      <div className="flex items-center gap-3 min-w-0">
        <span className="text-[10px] text-[rgba(245,247,251,0.4)] w-8 text-right shrink-0 tabular-nums">
          {visitCount}x
        </span>
        <div
          className="w-5 h-5 rounded-lg flex items-center justify-center shrink-0"
          style={{ backgroundColor: isWebUrl ? 'rgba(74,217,255,0.15)' : 'rgba(5,223,114,0.15)' }}
        >
          {isWebUrl ? (
            <ExternalLink className="w-3 h-3 text-[rgba(74,217,255,0.8)]" />
          ) : (
            <FolderOpen className="w-3 h-3 text-[rgba(5,223,114,0.8)]" />
          )}
        </div>
        <div className="flex-1 min-w-0">
          {isExpanded ? (
            <div className="space-y-2">
              {/* Título principal */}
              <div className="text-[13px] text-[#4ad9ff] font-semibold">
                {title}
              </div>
              {/* Caminho/Arquivo */}
              {filePath && (
                <div className="flex items-center gap-2 text-[11px] text-[rgba(245,247,251,0.6)] bg-[rgba(0,0,0,0.25)] px-2.5 py-1.5 rounded-md">
                  <FileText className="w-3 h-3 text-[rgba(5,223,114,0.7)] shrink-0" />
                  <span className="font-mono break-all">{filePath}</span>
                </div>
              )}
              {/* Path completo */}
              <div className="text-[10px] text-[rgba(245,247,251,0.35)] font-mono break-all">
                {path}
              </div>
            </div>
          ) : (
            <>
              {/* Título principal */}
              <div className="text-[12px] text-[rgba(245,247,251,0.85)] font-medium truncate">
                {title}
              </div>
              {/* Caminho/Arquivo */}
              {filePath && (
                <div className="text-[10px] text-[rgba(245,247,251,0.4)] truncate mt-0.5 font-mono flex items-center gap-1">
                  <FileText className="w-2.5 h-2.5 shrink-0" />
                  <span className="truncate">{filePath}</span>
                </div>
              )}
            </>
          )}
          <div className="flex items-center gap-2 mt-1">
            <span className="text-[10px] text-[rgba(245,247,251,0.3)]">
              via {sourceApp}
            </span>
            <span className="text-[9px] text-[rgba(74,217,255,0.5)] opacity-0 group-hover:opacity-100 transition-opacity">
              {isExpanded ? 'Clique para recolher' : 'Clique para expandir'}
            </span>
          </div>
        </div>
        <div className="w-16 h-2 bg-[rgba(255,255,255,0.05)] rounded-full overflow-hidden shrink-0">
          <div
            className="h-full rounded-full transition-all duration-300"
            style={{
              width: `${Math.min(percentage, 100)}%`,
              background: isWebUrl
                ? 'linear-gradient(90deg, rgba(74,217,255,0.6), rgba(74,217,255,0.3))'
                : 'linear-gradient(90deg, rgba(5,223,114,0.6), rgba(5,223,114,0.3))'
            }}
          />
        </div>
        <span className="text-[11px] text-[rgba(245,247,251,0.5)] w-12 text-right shrink-0 tabular-nums font-medium">
          {formatTime(totalSeconds)}
        </span>
        <div className="w-5 h-5 rounded flex items-center justify-center shrink-0">
          <span className="text-[9px] text-[rgba(245,247,251,0.5)]">
            {isExpanded ? '▲' : '▼'}
          </span>
        </div>
      </div>
    </div>
  );
}

export function TopPathsSection({
  paths,
  isLoading = false,
  title = 'URLs e Caminhos Mais Acessados',
  maxItems = 10,
}: TopPathsSectionProps) {
  const [showAll, setShowAll] = useState(false);

  const maxSeconds = useMemo(() => {
    if (paths.length === 0) return 1;
    return Math.max(...paths.map(p => p.totalSeconds), 1);
  }, [paths]);

  const displayedPaths = useMemo(() => {
    return showAll ? paths : paths.slice(0, maxItems);
  }, [paths, maxItems, showAll]);

  if (isLoading) {
    return (
      <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl">
        <CardHeader className="pb-2 pt-3 px-4">
          <CardTitle className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
            {title}
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-2 pb-3 px-4">
          <div className="flex items-center justify-center h-[200px]">
            <div className="w-6 h-6 border-2 border-[#4ad9ff] border-t-transparent rounded-full animate-spin" />
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl">
      <CardHeader className="pb-2 pt-3 px-4">
        <CardTitle className="flex items-center justify-between">
          <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">{title}</span>
          <span className="text-[10px] text-[rgba(245,247,251,0.4)]">
            {paths.length} itens
          </span>
        </CardTitle>
      </CardHeader>
      <CardContent className="pt-2 pb-3 px-4">
        {displayedPaths.length === 0 ? (
          <div className="flex items-center justify-center h-[120px] text-[rgba(245,247,251,0.4)] text-[12px]">
            Sem dados para exibir
          </div>
        ) : (
          <>
            <div className="space-y-0.5">
              {displayedPaths.map((path, index) => (
                <PathItem
                  key={`${path.path}-${index}`}
                  {...path}
                  maxSeconds={maxSeconds}
                />
              ))}
            </div>
            {paths.length > maxItems && (
              <button
                onClick={() => setShowAll(!showAll)}
                className="w-full mt-2 py-1 text-[10px] text-[rgba(74,217,255,0.7)] hover:text-[rgba(74,217,255,0.9)] flex items-center justify-center gap-1"
              >
                {showAll ? (
                  <>
                    <span>Ver menos</span>
                  </>
                ) : (
                  <>
                    <MoreHorizontal className="w-3 h-3" />
                    <span>Ver mais ({paths.length - maxItems} itens)</span>
                  </>
                )}
              </button>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
