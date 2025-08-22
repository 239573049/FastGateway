import { createRoot } from 'react-dom/client'
import App from './App.tsx'
import './index.css'
import { ThemeProvider } from './components/theme-provider.tsx'

createRoot(document.getElementById('root')!).render(
    <ThemeProvider defaultTheme="system" storageKey="fastgateway-theme">
        <App />
    </ThemeProvider>
)
