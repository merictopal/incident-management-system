import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vite';

// In development the browser talks only to the Vite server, which forwards
// API and SignalR (WebSocket) traffic to the API Gateway.
const gateway = process.env.API_GATEWAY_URL ?? 'http://localhost:5000';

export default defineConfig({
  plugins: [sveltekit()],
  server: {
    proxy: {
      '/api': { target: gateway, changeOrigin: true },
      '/hubs': { target: gateway, changeOrigin: true, ws: true }
    }
  }
});
