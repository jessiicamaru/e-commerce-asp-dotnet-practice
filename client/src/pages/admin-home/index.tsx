import { Navigate } from 'react-router-dom'
import { useAuth } from '@/context/auth/useAuth'
import { AdminOrdersPage } from '@/pages/admin-orders'

/**
 * Where the console opens (specs/043, 044): an administrator on the fulfilment queue, as before; a
 * moderator on theirs - the shops waiting for review.
 */
export function AdminHome() {
  const { isAdmin } = useAuth()
  return isAdmin ? <AdminOrdersPage /> : <Navigate to="/admin/shops" replace />
}
