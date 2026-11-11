import { useState } from 'react';
import { Outlet } from 'react-router-dom';

import { Sheet, SheetContent, SheetTitle } from '@/components/ui/sheet';

import { AppSidebar } from './AppSidebar';
import { AppTopbar } from './AppTopbar';

export function AppLayout() {
  const [isNavigationOpen, setIsNavigationOpen] = useState(false);
  const closeNavigation = () => setIsNavigationOpen(false);

  return (
    <div className="min-h-svh bg-background">
      <div className="flex">
        <aside className="fixed inset-y-0 left-0 z-30 hidden w-60 border-r border-sidebar-border lg:block">
          <AppSidebar />
        </aside>

        <div className="flex min-h-svh w-full flex-col lg:pl-60">
          <AppTopbar onOpenNavigation={() => setIsNavigationOpen(true)} />
          <main className="flex-1">
            <Outlet />
          </main>
        </div>
      </div>

      <Sheet open={isNavigationOpen} onOpenChange={setIsNavigationOpen}>
        <SheetContent
          side="left"
          className="w-72 max-w-[85vw] border-sidebar-border bg-sidebar p-0 text-sidebar-foreground"
        >
          <SheetTitle className="sr-only">Navegación</SheetTitle>
          <AppSidebar onNavigate={closeNavigation} />
        </SheetContent>
      </Sheet>
    </div>
  );
}
