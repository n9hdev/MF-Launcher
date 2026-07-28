export interface ApiEnvLike {
  VITE_API_BASE_URL?: string;
}

export function resolveApiBaseUrl(env: ApiEnvLike = import.meta.env): string {
  const configuredBaseUrl = env.VITE_API_BASE_URL?.trim();
  return configuredBaseUrl ? configuredBaseUrl.replace(/\/+$/, '') : 'http://25.20.173.193:5000';
}
