const DEFAULT_API_BASE_URL = 'http://localhost:8000/api/ocr/v1';
const mockSetting = import.meta.env.VITE_ENABLE_MOCKS;

export const env = Object.freeze({
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL || DEFAULT_API_BASE_URL,
  enableMocks: mockSetting ? mockSetting === 'true' : import.meta.env.DEV,
});
