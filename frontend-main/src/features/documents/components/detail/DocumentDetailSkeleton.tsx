export function DocumentDetailSkeleton() {
  return (
    <section
      role="status"
      aria-label="Cargando documento"
      className="space-y-6"
    >
      <span className="sr-only">Cargando documento…</span>

      <div className="animate-pulse space-y-3">
        <div className="h-3 w-24 rounded bg-muted" />
        <div className="h-9 w-2/3 rounded bg-muted" />
        <div className="h-5 w-56 rounded bg-muted" />
      </div>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_18rem]">
        <div className="space-y-6">
          <div className="animate-pulse rounded-xl border bg-card p-5 shadow-sm">
            <div className="h-5 w-48 rounded bg-muted" />
            <div className="mt-5 grid gap-4 sm:grid-cols-2">
              {Array.from({ length: 5 }, (_, index) => (
                <div key={index} className="space-y-2">
                  <div className="h-3 w-20 rounded bg-muted" />
                  <div className="h-4 w-32 rounded bg-muted" />
                </div>
              ))}
            </div>
          </div>

          <div className="animate-pulse rounded-xl border bg-card p-5 shadow-sm">
            <div className="h-5 w-36 rounded bg-muted" />
            <div className="mt-5 space-y-3">
              <div className="h-4 w-full rounded bg-muted" />
              <div className="h-4 w-11/12 rounded bg-muted" />
              <div className="h-4 w-4/5 rounded bg-muted" />
              <div className="h-4 w-2/3 rounded bg-muted" />
            </div>
          </div>
        </div>

        <div className="h-52 animate-pulse rounded-xl border bg-card shadow-sm" />
      </div>
    </section>
  );
}
