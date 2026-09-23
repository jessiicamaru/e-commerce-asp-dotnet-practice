import { Navigate } from 'react-router-dom'
import { useAuth } from '@/context/auth/useAuth'
import { AdminOrdersPage } from '@/pages/admin-orders'

/**
 * Where the console opens (specs/043, 044, 045): an administrator on the fulfilment queue, as before; a
 * moderator on their dashboard - what is waiting, and what they decided.
 */
export function AdminHome() {
  const { isAdmin } = useAuth()
  return isAdmin ? <AdminOrdersPage /> : <Navigate to="/admin/moderation" replace />
}
