import { NavLink } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { useAuth } from '@/context/auth/useAuth'
import { cn } from '@/utils/shared'

const link = ({ isActive }: { isActive: boolean }) =>
  cn('text-sm hover:underline', isActive && 'font-semibold')

export function TopBar() {
  const { user, restoring, signOut } = useAuth()

  return (
    <header className="bg-background/80 sticky top-0 z-10 border-b backdrop-blur">
      <div className="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-3 px-4 py-3">
        <NavLink to="/" className="text-base font-bold">
          e-commerce
        </NavLink>
        <nav className="flex flex-wrap items-center gap-4">
          <NavLink to="/status" className={link}>
            Status
          </NavLink>
          {restoring ? null : user ? (
            <>
              <NavLink to="/cart" className={link}>
                Cart
              </NavLink>
              <NavLink to="/orders" end className={link}>
                Orders
              </NavLink>
              <NavLink to="/account" className={link}>
                {user.firstName}
              </NavLink>
              <Button variant="ghost" size="sm" onClick={() => void signOut()}>
                Sign out
              </Button>
            </>
          ) : (
            <>
              <NavLink to="/sign-in" className={link}>
                Sign in
              </NavLink>
              <NavLink to="/sign-up" className={link}>
                Create account
              </NavLink>
            </>
          )}
        </nav>
      </div>
    </header>
  )
}
