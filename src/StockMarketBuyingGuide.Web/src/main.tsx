import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { GoogleOAuthProvider } from '@react-oauth/google'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import './index.css'
import App from './App.tsx'
import { logger, ConsoleTransport } from '@/lib/logger'

// To also ship logs to the backend, add an ApiTransport here:
//   import { ApiTransport } from '@/lib/logger'
//   new ApiTransport('/api/logs')
logger.configure({
  transports: [new ConsoleTransport()],
  minLevel: import.meta.env.DEV ? 'debug' : 'info',
})

const googleClientId = import.meta.env.VITE_GOOGLE_CLIENT_ID as string
const queryClient = new QueryClient()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <GoogleOAuthProvider clientId={googleClientId}>
      <QueryClientProvider client={queryClient}>
        <App />
      </QueryClientProvider>
    </GoogleOAuthProvider>
  </StrictMode>,
)
