import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { workspaceAliases } from '../../workspace.aliases.ts'

// The back office (specs/136, ADR-003): the staff's app, on an origin of its own. Like the storefront it talks ONLY to
// the gateway, through this proxy, so the browser sees one origin and no CORS.
//
// Open it at http://portal.localhost:5174 - a different HOST from the storefront's localhost, not only a different
// port. Cookies ignore the port: on one host the two apps' refresh cookies would be one cookie, and signing in to one
// would sign the other out (research D1). Browsers send *.localhost to this machine and treat it as secure, so
// Identity's Secure cookie is still accepted over plain HTTP.
const gateway = process.env.GATEWAY_URL ?? 'http://localhost:5000'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { alias: workspaceAliases(import.meta.dirname) },
  server: {
    port: 5174,
    strictPort: true,
    proxy: {
      '/api': { target: gateway, changeOrigin: false },
    },
  },
})
