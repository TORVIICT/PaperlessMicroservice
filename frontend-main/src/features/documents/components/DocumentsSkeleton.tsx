const SKELETON_ROW_COUNT = 5;

const SKELETON_ROWS = Array.from(
  { length: SKELETON_ROW_COUNT },
  (_, index) => index,
);

export function DocumentsSkeleton() {
  return (
    <section
      role="status"
      aria-label="Cargando documentos"
      className="space-y-3"
    >
      <span className="sr-only">Cargando documentos…</span>
      <div className="space-y-3">
        {SKELETON_ROWS.map((row) => (
          <div
            key={row}
            className="flex animate-pulse items-center gap-4 rounded-xl border bg-card p-4 shadow-xs"
          >
            <div className="size-14 shrink-0 rounded-xl bg-muted" />
            <div className="flex-1 space-y-2">
              <div className="h-3.5 w-1/3 rounded bg-muted" />
              <div className="h-3 w-24 rounded bg-muted" />
            </div>
            <div className="hidden h-5 w-20 rounded-full bg-muted md:block" />
          </div>
        ))}
      </div>
    </section>
  );
}
