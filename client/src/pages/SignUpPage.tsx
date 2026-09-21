import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ApiError } from '../api/http'
import { useAuth } from '../auth/useAuth'

export function SignUpPage() {
  const { signUp } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({ firstName: '', lastName: '', email: '', password: '' })
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState(false)

  const set = (key: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm({ ...form, [key]: e.target.value })

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    setFieldErrors({})
    try {
      await signUp(form)
      navigate('/')
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        setError('An account with this email already exists. Sign in instead.')
      } else if (err instanceof ApiError && err.status === 400) {
        setFieldErrors(err.fieldErrors)
        setError('Some details need fixing.')
      } else {
        setError('Creating the account failed. Try again in a moment.')
      }
    } finally {
      setBusy(false)
    }
  }

  const field = (key: keyof typeof form, label: string, type = 'text', autoComplete?: string) => {
    const serverKey = key.charAt(0).toUpperCase() + key.slice(1)
    return (
      <label>
        {label}
        <input type={type} required autoComplete={autoComplete} value={form[key]} onChange={set(key)} />
        {fieldErrors[serverKey] && <span className="error">{fieldErrors[serverKey]}</span>}
      </label>
    )
  }

  return (
    <section className="narrow">
      <h1>Create an account</h1>
      <form onSubmit={submit} className="form">
        {field('firstName', 'First name', 'text', 'given-name')}
        {field('lastName', 'Last name', 'text', 'family-name')}
        {field('email', 'Email', 'email', 'email')}
        {field('password', 'Password', 'password', 'new-password')}
        {error && <p className="error" role="alert">{error}</p>}
        <button disabled={busy}>{busy ? 'Creating…' : 'Create account'}</button>
      </form>
      <p className="muted">
        Already have one? <Link to="/sign-in">Sign in</Link>.
      </p>
    </section>
  )
}
