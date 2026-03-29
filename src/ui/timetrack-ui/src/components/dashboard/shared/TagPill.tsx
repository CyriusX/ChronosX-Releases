/**
 * TagPill - Colored tag badge component
 */

interface TagPillProps {
  label: string;
  color: string;
  size?: 'sm' | 'md';
  onRemove?: () => void;
}

export function TagPill({ label, color, size = 'sm', onRemove }: TagPillProps) {
  const sizeClasses = size === 'sm'
    ? 'px-2 py-0.5 text-[9px]'
    : 'px-2.5 py-1 text-[10px]';

  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full font-medium ${sizeClasses}`}
      style={{
        backgroundColor: `${color}18`,
        border: `1px solid ${color}40`,
        color: color,
      }}
    >
      {label}
      {onRemove && (
        <button
          onClick={onRemove}
          className="ml-0.5 opacity-60 hover:opacity-100 transition-opacity"
        >
          &times;
        </button>
      )}
    </span>
  );
}
