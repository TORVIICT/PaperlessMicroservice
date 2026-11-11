import { cn } from '@/lib/utils';

interface AutomaticUpdateIndicatorProps {
  className?: string;
}

export function AutomaticUpdateIndicator({
  className,
}: AutomaticUpdateIndicatorProps) {
  return (
    <p
      role="status"
      className={cn(
        'inline-flex items-center gap-2 text-xs text-muted-foreground',
        className,
      )}
    >
      <span
        aria-hidden="true"
        className="size-1.5 animate-pulse rounded-full bg-status-completed"
      />
      Actualización automática activa
    </p>
  );
}
