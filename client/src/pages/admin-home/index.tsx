import { Navigate } from 'react-router-dom'
import { useAuth } from '@/context/auth/useAuth'
import { AdminOrdersPage } from '@/pages/admin-orders'

/**
 * Where the console opens (specs/043): an administrator on the fulfilment queue, as before; a moderator,
 * who has no queue to work yet, on the people they look after.
 */
export function AdminHome() {
  const { isAdmin } = useAuth()
  return isAdmin ? <AdminOrdersPage /> : <Navigate to="/admin/users" replace />
}
