import { QueryClientProvider } from '@tanstack/react-query'
import { queryClient } from '@/config/query-client'
import { AuthProvider } from '@/context/auth'
import { AppRoutes } from '@/routes'

// A deliberately thin storefront (issue #23): its job is to exercise the API the way a person would,
// and to surface what the backend lacks - not to be polished.
//
// QueryClientProvider is outermost because AuthProvider clears the cache on sign-out: whose cart and
// orders are held has to change with who is signed in.
export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </QueryClientProvider>
  )
}
