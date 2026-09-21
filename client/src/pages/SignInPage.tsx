import { useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { ApiError } from '../api/http'
import { useAuth } from '../auth/useAuth'

export function SignInPage() {
  const { signIn } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await signIn(email, password)
      navigate((location.state as { from?: string } | null)?.from ?? '/')
    } catch (err) {
      // Identity answers 401 with one message for "no such email" and "wrong password" (#28), so the
      // page does too - it must not reveal which emails have accounts.
      setError(
        err instanceof ApiError && err.status === 401
          ? 'That email and password do not match an account.'
          : 'Signing in failed. Try again in a moment.',
      )
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="narrow">
      <h1>Sign in</h1>
      <form onSubmit={submit} className="form">
        <label>
          Email
          <input type="email" autoComplete="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
        </label>
        <label>
          Password
          <input type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
        </label>
        {error && <p className="error" role="alert">{error}</p>}
        <button disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</button>
      </form>
      <p className="muted">
        No account? <Link to="/sign-up">Create one</Link>.
      </p>
    </section>
  )
}
