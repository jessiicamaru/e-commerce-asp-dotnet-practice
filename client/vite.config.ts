import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The storefront talks ONLY to the gateway (feature 014). In development Vite proxies /api to it, so
// the browser sees one origin: no CORS, and the refresh-token cookie Identity sets is a same-origin,
// HttpOnly cookie exactly as it would be behind a real reverse proxy.
const gateway = process.env.GATEWAY_URL ?? 'http://localhost:5000'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: gateway, changeOrigin: false },
    },
  },
})
