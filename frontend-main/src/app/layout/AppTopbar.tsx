import { Menu, Search } from 'lucide-react';

import { Button } from '@/components/ui/button';

import { demoUser } from './demoUser';

interface AppTopbarProps {
  onOpenNavigation: () => void;
}

export function AppTopbar({ onOpenNavigation }: AppTopbarProps) {
  return (
    <header className="sticky top-0 z-20 flex h-16 items-center gap-3 border-b border-border bg-card/85 px-4 backdrop-blur sm:px-6">
      <Button
        type="button"
        variant="ghost"
        size="icon"
        className="lg:hidden"
        aria-label="Abrir navegación"
        onClick={onOpenNavigation}
      >
        <Menu aria-hidden="true" />
      </Button>

      <div className="relative hidden flex-1 items-center sm:flex">
        <Search
          aria-hidden="true"
          className="pointer-events-none absolute left-3 size-4 text-muted-foreground"
        />
        <input
          type="search"
          disabled
          placeholder="Buscar documentos..."
          aria-label="Buscar documentos"
          title="Búsqueda disponible próximamente"
          className="h-9 w-full max-w-md rounded-lg border border-input bg-card pl-9 pr-3 text-sm text-foreground placeholder:text-muted-foreground/70 focus:outline-none disabled:cursor-not-allowed disabled:opacity-100"
        />
      </div>

      <Button
        type="button"
        variant="ghost"
        size="icon"
        className="sm:hidden"
        disabled
        aria-label="Buscar documentos"
        title="Búsqueda disponible próximamente"
      >
        <Search aria-hidden="true" />
      </Button>

      <div className="ml-auto flex items-center gap-3">
        <div className="hidden text-right sm:block">
          <p className="text-sm font-medium leading-tight">{demoUser.name}</p>
          <p className="text-xs text-muted-foreground">{demoUser.role}</p>
        </div>
        <span className="grid size-9 place-items-center rounded-full bg-primary/10 text-sm font-semibold text-primary">
          {demoUser.initials}
          <span className="sr-only sm:hidden">
            {`${demoUser.name} — ${demoUser.role}`}
          </span>
        </span>
      </div>
    </header>
  );
}
