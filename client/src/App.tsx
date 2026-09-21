import { BrowserRouter, Link, NavLink, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import { useAuth } from './auth/useAuth'
import { RequireAuth } from './auth/RequireAuth'
import { AddressesPage } from './pages/AddressesPage'
import { CartPage } from './pages/CartPage'
import { CatalogPage } from './pages/CatalogPage'
import { ProductPage } from './pages/ProductPage'
import { SignInPage } from './pages/SignInPage'
import { SignUpPage } from './pages/SignUpPage'
import { StatusPage } from './pages/StatusPage'

// A deliberately thin storefront (issue #23): its job is to exercise the API the way a person would,
// and to surface what the backend lacks - not to be polished. Pages arrive with #35-#39.
export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <TopBar />
        <main>
          <Routes>
            <Route path="/" element={<CatalogPage />} />
            <Route path="/products/:id" element={<ProductPage />} />
            <Route path="/sign-in" element={<SignInPage />} />
            <Route path="/sign-up" element={<SignUpPage />} />
            <Route path="/account" element={<RequireAuth><Account /></RequireAuth>} />
            <Route path="/cart" element={<RequireAuth><CartPage /></RequireAuth>} />
            <Route path="/addresses" element={<RequireAuth><AddressesPage /></RequireAuth>} />
            <Route path="/status" element={<StatusPage />} />
            <Route path="*" element={<p>Not found.</p>} />
          </Routes>
        </main>
      </BrowserRouter>
    </AuthProvider>
  )
}

function TopBar() {
  const { user, restoring, signOut } = useAuth()
  return (
    <header className="topbar">
      <NavLink to="/" className="brand">
        e-commerce
      </NavLink>
      <nav>
        <NavLink to="/status">Status</NavLink>
        {restoring ? null : user ? (
          <>
            <NavLink to="/cart">Cart</NavLink>
            <NavLink to="/account">{user.firstName}</NavLink>
            <button className="link" onClick={() => void signOut()}>
              Sign out
            </button>
          </>
        ) : (
          <>
            <NavLink to="/sign-in">Sign in</NavLink>
            <NavLink to="/sign-up">Create account</NavLink>
          </>
        )}
      </nav>
    </header>
  )
}

function Account() {
  const { user } = useAuth()
  return (
    <section>
      <h1>Your account</h1>
      <p>
        {user?.firstName} {user?.lastName} · {user?.email}
      </p>
      <p className="muted">Signed in. Reload the page: you stay signed in, and no token is stored anywhere a script can read.</p>
      <p>
        <Link to="/addresses">Delivery addresses</Link> · <Link to="/cart">Cart</Link>
      </p>
    </section>
  )
}
