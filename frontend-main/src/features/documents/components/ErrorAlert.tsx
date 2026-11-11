import type { ReactNode } from 'react';
import { AlertTriangle } from 'lucide-react';

import { cn } from '@/lib/utils';

interface ErrorAlertProps {
  title: string;
  code: string;
  message: string;
  action?: ReactNode;
  className?: string;
}

export function ErrorAlert({
  title,
  code,
  message,
  action,
  className,
}: ErrorAlertProps) {
  return (
    <div
      role="alert"
      className={cn(
        'flex gap-3 rounded-lg border border-destructive/20 bg-destructive/5 p-3',
        className,
      )}
    >
      <AlertTriangle
        aria-hidden="true"
        className="mt-0.5 size-4 shrink-0 text-destructive"
      />
      <div className="min-w-0 space-y-1">
        <p className="text-sm font-medium">{title}</p>
        <p className="text-sm text-muted-foreground">{message}</p>
        <p className="text-xs text-muted-foreground">
          Código:{' '}
          <code className="rounded bg-muted px-1.5 py-0.5 font-mono">
            {code}
          </code>
        </p>
        {action ? <div className="pt-2">{action}</div> : null}
      </div>
    </div>
  );
}
