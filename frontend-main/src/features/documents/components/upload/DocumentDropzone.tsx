import { useId, useRef, useState } from 'react';
import type { ChangeEvent, DragEvent } from 'react';
import { Upload } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

import { formatContentTypeList } from '../../lib/documentFormatters';
import {
  MAX_FILE_SIZE_MB,
  SUPPORTED_CONTENT_TYPES,
} from '../../models/documentConstraints';

const SUPPORTED_FORMATS_LABEL = formatContentTypeList(SUPPORTED_CONTENT_TYPES);

interface DocumentDropzoneProps {
  disabled?: boolean;
  onFilesSelected: (files: File[]) => void;
}

export function DocumentDropzone({
  disabled = false,
  onFilesSelected,
}: DocumentDropzoneProps) {
  const inputId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const dragDepth = useRef(0);
  const [isDragging, setIsDragging] = useState(false);

  function handleInputChange(event: ChangeEvent<HTMLInputElement>) {
    const files = Array.from(event.target.files ?? []);

    if (files.length > 0) {
      onFilesSelected(files);
    }

    event.target.value = '';
  }

  function handleDragEnter(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();

    if (disabled) {
      return;
    }

    dragDepth.current += 1;
    setIsDragging(true);
  }

  function handleDragLeave(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();

    if (disabled) {
      dragDepth.current = 0;
      setIsDragging(false);
      return;
    }

    dragDepth.current = Math.max(0, dragDepth.current - 1);

    if (dragDepth.current === 0) {
      setIsDragging(false);
    }
  }

  function handleDragOver(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();

    if (!disabled) {
      event.dataTransfer.dropEffect = 'copy';
    }
  }

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    dragDepth.current = 0;
    setIsDragging(false);

    if (disabled) {
      return;
    }

    const files = Array.from(event.dataTransfer.files);

    if (files.length > 0) {
      onFilesSelected(files);
    }
  }

  function openFilePicker() {
    inputRef.current?.click();
  }

  return (
    <div
      role="group"
      aria-label="Carga de documentos"
      onDragEnter={handleDragEnter}
      onDragLeave={handleDragLeave}
      onDragOver={handleDragOver}
      onDrop={handleDrop}
      className={cn(
        'rounded-xl border-2 border-dashed bg-muted/15 px-6 py-10 text-center transition-[border-color,background-color,box-shadow]',
        isDragging && 'border-primary bg-primary/5 shadow-inner',
        !isDragging && !disabled && 'hover:border-primary/40 hover:bg-primary/[0.03]',
        disabled && 'opacity-60',
      )}
    >
      <input
        ref={inputRef}
        id={inputId}
        type="file"
        multiple
        accept={SUPPORTED_CONTENT_TYPES.join(',')}
        aria-describedby={`${inputId}-help`}
        disabled={disabled}
        onChange={handleInputChange}
        className="sr-only"
      />
      <label htmlFor={inputId} className="sr-only">
        Seleccionar documentos
      </label>

      <span
        aria-hidden="true"
        className="mx-auto grid size-12 place-items-center rounded-xl border bg-background text-primary shadow-xs"
      >
        <Upload className="size-5" />
      </span>
      <p className="mt-3 text-sm font-medium">
        {isDragging
          ? 'Soltá el archivo para cargarlo'
          : 'Arrastrá y soltá tus documentos'}
      </p>
      <p className="mt-1 text-xs text-muted-foreground">
        o seleccionalo desde tu equipo
      </p>
      <Button
        type="button"
        variant="outline"
        size="sm"
        className="mt-4"
        onClick={openFilePicker}
        disabled={disabled}
      >
        Seleccionar archivos
      </Button>
      <p
        id={`${inputId}-help`}
        className="mt-4 text-xs text-muted-foreground"
      >
        {SUPPORTED_FORMATS_LABEL} · Máximo {MAX_FILE_SIZE_MB} MB
      </p>
    </div>
  );
}
