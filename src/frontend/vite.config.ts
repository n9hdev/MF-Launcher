import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';

const devPort = Number(process.env.VITE_PORT || 5173);

export default defineConfig({
  plugins: [react()],
  base: './',
  resolve: {
    alias: {
      '@': path.resolve(__dirname, 'src/renderer'),
      '@main': path.resolve(__dirname, 'src/main'),
      '@shared': path.resolve(__dirname, 'src/shared'),
    },
  },
  build: {
    outDir: 'dist/renderer',
    emptyOutDir: true,
    cssMinify: 'esbuild',
    minify: 'esbuild',
    target: 'es2022',
    sourcemap: false,
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (id.includes('node_modules')) {
            if (id.includes('react') || id.includes('react-dom') || id.includes('react-router-dom')) {
              return 'vendor';
            }
            if (id.includes('framer-motion')) {
              return 'motion';
            }
            if (id.includes('@microsoft/signalr')) {
              return 'signalr';
            }
            if (id.includes('lucide-react') || id.includes('zustand')) {
              return 'ui';
            }
          }
        },
      },
    },
  },
  server: {
    host: '0.0.0.0',
    port: devPort,
    strictPort: true,
  },
  test: {
    globals: true,
    environment: 'happy-dom',
    setupFiles: ['./src/renderer/tests/setup.ts'],
    include: ['src/renderer/tests/**/*.test.{ts,tsx}'],
  },
});
