import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { resolve } from 'path'

export default defineConfig({
  resolve: {
    alias: {
      '@': resolve(__dirname, './src'),
    },
    extensions: ['.js', '.jsx', '.ts', '.tsx']
  },
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: 'http://192.168.31.138:18080',
        changeOrigin: true,
        rewrite: (path) => path
      }
    }
  }
})
