import { BrowserRouter, NavLink, Route, Routes } from 'react-router-dom'
import { StatusPage } from './pages/StatusPage'

// A deliberately thin storefront (issue #23): its job is to exercise the API the way a person would,
// and to surface what the backend lacks - not to be polished. Pages arrive with #35-#39.
export default function App() {
  return (
    <BrowserRouter>
      <header className="topbar">
        <NavLink to="/" className="brand">
          e-commerce
        </NavLink>
        <nav>
          <NavLink to="/status">Status</NavLink>
        </nav>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/status" element={<StatusPage />} />
          <Route path="*" element={<p>Not found.</p>} />
        </Routes>
      </main>
    </BrowserRouter>
  )
}

function Home() {
  return (
    <section>
      <h1>Storefront</h1>
      <p className="muted">The shop is being built one issue at a time. Check the backend on the Status page.</p>
    </section>
  )
}
