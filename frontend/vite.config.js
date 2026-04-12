import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
export default defineConfig({
  plugins: [react()],
  server: { proxy: { '/api': { target: 'https://xecm-production.up.railway.app', changeOrigin: true, rewrite: (p) => p.replace(/^\/api/, '') } } }
})
