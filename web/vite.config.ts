import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from "path"
import tailwindcss from "@tailwindcss/vite"

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
      "@animate-ui/components-animate-avatar-group": path.resolve(
        __dirname,
        "./src/components/animate-ui/components/animate/avatar-group.tsx",
      ),
    },
  },
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5202'
      }
    }
  }
})
