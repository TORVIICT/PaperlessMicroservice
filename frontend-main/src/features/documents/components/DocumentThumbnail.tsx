import { FileImage, FileText } from 'lucide-react';

import { cn } from '@/lib/utils';

interface DocumentThumbnailProps {
  contentType: string;
  className?: string;
}

export function DocumentThumbnail({
  contentType,
  className,
}: DocumentThumbnailProps) {
  const isPdf = contentType.toLowerCase().includes('pdf');
  const Icon = isPdf ? FileText : FileImage;

  return (
    <span
      aria-hidden="true"
      className={cn(
        'relative grid size-14 shrink-0 place-items-center overflow-hidden rounded-xl border bg-linear-to-br from-muted/30 to-muted text-muted-foreground',
        className,
      )}
    >
      <Icon className="size-6" />
      <span className="absolute inset-x-0 bottom-0 bg-foreground/70 py-0.5 text-center text-[9px] font-bold tracking-wider text-background">
        {isPdf ? 'PDF' : 'IMG'}
      </span>
    </span>
  );
}
