import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';

import { AppLayout } from '../layout/AppLayout';
import { DocumentDetailPage } from '../../features/documents/pages/DocumentDetailPage';
import { DocumentsPage } from '../../features/documents/pages/DocumentsPage';
import { ROUTES } from '../../shared/routes';

export function AppRouter() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<AppLayout />}>
          <Route path={ROUTES.documents} element={<DocumentsPage />} />
          <Route path={ROUTES.documentDetail} element={<DocumentDetailPage />} />
        </Route>
        <Route
          path="*"
          element={<Navigate to={ROUTES.documents} replace />}
        />
      </Routes>
    </BrowserRouter>
  );
}
