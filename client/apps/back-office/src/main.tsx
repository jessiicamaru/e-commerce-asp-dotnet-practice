import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import '@ecommerce/core/config/i18n'
import App from '@/App'
import { setCurrentApp } from '@ecommerce/core/config/apps'

setCurrentApp('back-office')

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
