import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { ApiError, NETWORK_ERROR_STATUS } from '@/shared/api/ApiError';
import { httpClient } from '@/shared/api/httpClient';
import { env } from '@/shared/config/env';

import { server } from '../msw/server';

const API_URL = env.apiBaseUrl.replace(/\/$/, '');

async function captureError(promise: Promise<unknown>): Promise<unknown> {
  try {
    await promise;
  } catch (error) {
    return error;
  }

  throw new Error('Se esperaba que la petición fallara.');
}

describe('httpClient', () => {
  it('returns parsed JSON responses', async () => {
    server.use(
      http.get(`${API_URL}/ping`, () => HttpResponse.json({ ok: true })),
    );

    await expect(httpClient.json('ping')).resolves.toEqual({ ok: true });
  });

  it('sends the JSON Accept header', async () => {
    let receivedAccept: string | null = null;

    server.use(
      http.get(`${API_URL}/accept`, ({ request }) => {
        receivedAccept = request.headers.get('accept');
        return HttpResponse.json({ ok: true });
      }),
    );

    await httpClient.json('accept');

    expect(receivedAccept).toBe('application/json');
  });

  it('preserves the API error payload', async () => {
    server.use(
      http.get(`${API_URL}/broken`, () =>
        HttpResponse.json(
          {
            code: 'DOCUMENT_NOT_FOUND',
            message: 'El documento no existe.',
            documentId: 'abc',
            details: { field: 'id' },
          },
          { status: 404 },
        ),
      ),
    );

    const error = await captureError(httpClient.json('broken'));

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({
      status: 404,
      code: 'DOCUMENT_NOT_FOUND',
      documentId: 'abc',
      details: { field: 'id' },
    });
  });

  it('falls back to UNEXPECTED_ERROR for invalid error payloads', async () => {
    server.use(
      http.get(`${API_URL}/invalid-error`, () =>
        HttpResponse.json({ unexpected: true }, { status: 400 }),
      ),
    );

    const error = await captureError(httpClient.json('invalid-error'));

    expect(error).toMatchObject({
      status: 400,
      code: 'UNEXPECTED_ERROR',
      details: {},
    });
  });

  it('falls back to SERVICE_UNAVAILABLE for 5xx responses without a payload', async () => {
    server.use(
      http.get(
        `${API_URL}/html-error`,
        () =>
          new HttpResponse('<html>error</html>', {
            status: 503,
            headers: { 'Content-Type': 'text/html' },
          }),
      ),
    );

    const error = await captureError(httpClient.json('html-error'));

    expect(error).toMatchObject({
      status: 503,
      code: 'SERVICE_UNAVAILABLE',
    });
  });

  it('maps network failures to SERVICE_UNAVAILABLE', async () => {
    server.use(
      http.get(`${API_URL}/offline`, () => HttpResponse.error()),
    );

    const error = await captureError(httpClient.json('offline'));

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({
      status: NETWORK_ERROR_STATUS,
      code: 'SERVICE_UNAVAILABLE',
    });
  });

  it('propagates abort errors instead of wrapping them', async () => {
    server.use(
      http.get(`${API_URL}/aborted`, () => HttpResponse.json({ ok: true })),
    );

    const controller = new AbortController();
    controller.abort();

    await expect(
      httpClient.json('aborted', { signal: controller.signal }),
    ).rejects.toMatchObject({ name: 'AbortError' });
  });
});
