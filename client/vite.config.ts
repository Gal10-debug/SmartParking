import { fileURLToPath } from 'node:url';
import { loadEnv, type PreviewServer, type ViteDevServer } from 'vite';
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

const clientDirectory = fileURLToPath(new URL('.', import.meta.url));

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, clientDirectory, '');
  const proxy = env.API_PROXY_TARGET
    ? { '/api': { target: env.API_PROXY_TARGET, changeOrigin: true } }
    : undefined;

  function guardApiRoutes(server: ViteDevServer | PreviewServer) {
    if (proxy || env.VITE_API_BASE_URL) return;
    server.middlewares.use('/api', (_request, response) => {
      response.statusCode = 503;
      response.setHeader('Content-Type', 'application/problem+json');
      response.end(
        JSON.stringify({
          title: 'API connection is not configured',
          detail:
            'Set API_PROXY_TARGET in client/.env to the running server address, then restart the client.',
          status: 503,
        }),
      );
    });
  }

  return {
    envDir: clientDirectory,
    plugins: [
      react(),
      {
        name: 'require-api-connection',
        configureServer: guardApiRoutes,
        configurePreviewServer: guardApiRoutes,
      },
    ],
    server: { proxy },
    preview: { proxy },
    test: {
      environment: 'jsdom',
      setupFiles: './src/test-setup.ts',
      css: false,
    },
  };
});
