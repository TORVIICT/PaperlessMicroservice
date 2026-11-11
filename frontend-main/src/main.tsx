import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import { App } from './app/App';
import { env } from './shared/config/env';
import './shared/styles/global.css';

async function enableApiMocking() {
  if (!env.enableMocks) {
    return;
  }

  const { worker } = await import('./mocks/browser');
  await worker.start({ onUnhandledFrame: 'bypass' });
}

function renderApplication() {
  const rootElement = document.getElementById('root');

  if (!rootElement) {
    throw new Error('No se encontró el elemento raíz de la aplicación.');
  }

  createRoot(rootElement).render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
}

enableApiMocking().then(renderApplication).catch((error: unknown) => {
  console.error('No fue posible iniciar la aplicación.', error);
});
