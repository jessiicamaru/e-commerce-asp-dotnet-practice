import { QueryClientProvider } from '@tanstack/react-query'
import { queryClient } from '@ecommerce/core/config/query-client'
import { AuthProvider } from '@ecommerce/core/context/auth'
import { Toaster } from '@ecommerce/ui/sonner'
import { TooltipProvider } from '@ecommerce/ui/tooltip'
import { AppRoutes } from '@/routes'

// The back office (specs/136, ADR-003): where administrators and moderators run the shop, apart from the storefront.
// The same providers, in the same order, as the storefront: AuthProvider clears the query cache on sign-out, so it sits
// inside the client it clears. Its session is this app's own - Identity's refresh cookie on this host.
export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <TooltipProvider delay={300}>
          <AppRoutes />
          <Toaster />
        </TooltipProvider>
      </AuthProvider>
    </QueryClientProvider>
  )
}
