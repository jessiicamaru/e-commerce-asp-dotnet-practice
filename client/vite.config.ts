import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The storefront talks ONLY to the gateway (feature 014). In development Vite proxies /api to it, so
// the browser sees one origin: no CORS, and the refresh-token cookie Identity sets is a same-origin,
// HttpOnly cookie exactly as it would be behind a real reverse proxy.
const gateway = process.env.GATEWAY_URL ?? 'http://localhost:5000'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  // "@/..." means src/ - the alias shadcn/ui generates imports against.
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
      // shadcn/ui's base-nova components import { cn } from "cn". Mapping it here means a generated
      // component needs no editing, so `shadcn add` stays usable.
      cn: path.resolve(import.meta.dirname, './src/utils/shared/cn.ts'),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': { target: gateway, changeOrigin: false },
    },
  },
})
