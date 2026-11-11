import type { ReactNode } from 'react';
import { Files, ScanText } from 'lucide-react';
import { Link, useLocation, useSearchParams } from 'react-router-dom';

import { cn } from '@/lib/utils';
import { DocumentStatusDot } from '@/features/documents/components/DocumentStatusDot';
import { parseDocumentListState } from '@/features/documents/lib/listParams';
import { DOCUMENT_STATUSES } from '@/features/documents/models/document';
import { getDocumentStatusPresentation } from '@/features/documents/lib/statusLabels';
import { ROUTES } from '@/shared/routes';

interface AppSidebarProps {
  onNavigate?: () => void;
}

interface SidebarLinkProps {
  to: string;
  isActive: boolean;
  left: ReactNode;
  children: ReactNode;
  onNavigate?: () => void;
}

const STATUS_ITEMS = DOCUMENT_STATUSES.map((status) => ({
  status,
  label: getDocumentStatusPresentation(status).label,
}));

function SidebarLink({
  to,
  isActive,
  left,
  children,
  onNavigate,
}: SidebarLinkProps) {
  return (
    <Link
      to={to}
      aria-current={isActive ? 'page' : undefined}
      onClick={onNavigate}
      className={cn(
        'flex h-9 items-center gap-2 rounded-lg px-3 text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sidebar-ring',
        isActive
          ? 'bg-sidebar-accent text-sidebar-accent-foreground'
          : 'text-sidebar-foreground/70 hover:bg-sidebar-accent/50 hover:text-sidebar-foreground',
      )}
    >
      {left}
      <span className="min-w-0 truncate">{children}</span>
    </Link>
  );
}

export function AppSidebar({ onNavigate }: AppSidebarProps) {
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const { status } = parseDocumentListState(searchParams);

  const isDocumentArea =
    location.pathname === ROUTES.documents ||
    location.pathname.startsWith(`${ROUTES.documents}/`);
  const isOnDocumentsList = location.pathname === ROUTES.documents;
  const isDocumentsActive = isDocumentArea && !status;

  return (
    <div className="flex h-svh flex-col overflow-y-auto bg-sidebar text-sidebar-foreground">
      <div className="flex items-center gap-2.5 px-4 pb-4 pt-5">
        <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-primary text-primary-foreground">
          <ScanText aria-hidden="true" className="size-5" />
        </span>
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold">OCR Documentos</p>
          <p className="truncate text-xs text-sidebar-foreground/60">
            Gestión documental
          </p>
        </div>
      </div>

      <nav
        aria-label="Navegación principal"
        className="flex-1 space-y-5 px-3 pb-4"
      >
        <div className="space-y-1">
          <SidebarLink
            to={ROUTES.documents}
            isActive={isDocumentsActive}
            left={<Files aria-hidden="true" className="size-4" />}
            onNavigate={onNavigate}
          >
            Documentos
          </SidebarLink>
        </div>

        <div className="space-y-1">
          <p className="px-3 pb-1 text-[11px] font-semibold uppercase tracking-wide text-sidebar-foreground/50">
            Estados
          </p>
          {STATUS_ITEMS.map(({ status: itemStatus, label }) => {
            const isActive =
              isOnDocumentsList && status === itemStatus;

            return (
              <SidebarLink
                key={itemStatus}
                to={`${ROUTES.documents}?status=${itemStatus}`}
                isActive={isActive}
                left={
                  <DocumentStatusDot
                    status={itemStatus}
                    className="mx-1.5"
                  />
                }
                onNavigate={onNavigate}
              >
                {label}
              </SidebarLink>
            );
          })}
        </div>
      </nav>
    </div>
  );
}
