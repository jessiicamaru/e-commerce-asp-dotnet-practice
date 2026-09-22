import { Link } from 'react-router-dom'
import { useAuth } from '@/context/auth/useAuth'

export function AccountPage() {
  const { user } = useAuth()

  return (
    <section>
      <h1 className="mb-4 text-2xl font-bold">Your account</h1>
      <p>
        {user?.firstName} {user?.lastName} · {user?.email}
      </p>
      <p className="text-muted-foreground mt-2 text-sm">
        Signed in. Reload the page: you stay signed in, and no token is stored anywhere a script can read.
      </p>
      <p className="mt-4 flex gap-3 text-sm">
        <Link to="/addresses" className="underline">
          Delivery addresses
        </Link>
        <Link to="/cart" className="underline">
          Cart
        </Link>
        <Link to="/orders" className="underline">
          Orders
        </Link>
      </p>
    </section>
  )
}
