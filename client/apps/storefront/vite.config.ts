import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { workspaceAliases } from '../../workspace.aliases.ts'

// The storefront talks ONLY to the gateway (feature 014). In development Vite proxies /api to it, so
// the browser sees one origin: no CORS, and the refresh-token cookie Identity sets is a same-origin,
// HttpOnly cookie exactly as it would be behind a real reverse proxy.
const gateway = process.env.GATEWAY_URL ?? 'http://localhost:5000'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  // "@/" is this app's src; the shared packages by name (specs/135). See workspace.aliases.ts.
  resolve: { alias: workspaceAliases(import.meta.dirname) },
  server: {
    port: 5173,
    proxy: {
      '/api': { target: gateway, changeOrigin: false },
    },
  },
})
