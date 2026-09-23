import { QueryClientProvider } from '@tanstack/react-query'
import { queryClient } from '@/config/query-client'
import { TooltipProvider } from '@/components/ui/tooltip'
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
        {/* One provider for every tooltip, so hovering from one icon to the next opens instantly
            instead of waiting out the delay again. */}
        <TooltipProvider delay={300}>
          <AppRoutes />
        </TooltipProvider>
      </AuthProvider>
    </QueryClientProvider>
  )
}
