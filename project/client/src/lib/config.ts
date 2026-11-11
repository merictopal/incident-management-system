import { env } from '$env/dynamic/public';

/**
 * API Gateway address as seen from the browser.
 * - Development (`npm run dev`): leave PUBLIC_API_URL unset. Requests go to the same origin
 *   and the Vite dev server proxies /api and /hubs to the gateway (see vite.config.ts).
 * - Docker / production: PUBLIC_API_URL=http://localhost:5000 (the gateway allows this origin via CORS).
 */
export const apiUrl = (env.PUBLIC_API_URL ?? '').replace(/\/+$/, '');
