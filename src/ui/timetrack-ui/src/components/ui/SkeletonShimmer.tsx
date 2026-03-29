interface SkeletonShimmerProps {
  width?: string | number;
  height?: string | number;
  rounded?: string;
  className?: string;
}

export function SkeletonShimmer({
  width = '100%',
  height = 20,
  rounded = 'rounded-lg',
  className = '',
}: SkeletonShimmerProps) {
  return (
    <div
      className={`animate-shimmer bg-gradient-to-r from-[rgba(139,92,246,0.03)] via-[rgba(139,92,246,0.10)] to-[rgba(139,92,246,0.03)] bg-[length:200%_100%] ${rounded} ${className}`}
      style={{ width, height }}
    />
  );
}
