import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ApiError } from '@/config/axios'
import { ErrorMessage } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAuth } from '@/context/auth/useAuth'

const FIELDS = [
  { name: 'firstName', label: 'First name', type: 'text', autoComplete: 'given-name' },
  { name: 'lastName', label: 'Last name', type: 'text', autoComplete: 'family-name' },
  { name: 'email', label: 'Email', type: 'email', autoComplete: 'email' },
  { name: 'password', label: 'Password', type: 'password', autoComplete: 'new-password' },
] as const

export function SignUpPage() {
  const { signUp } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({ firstName: '', lastName: '', email: '', password: '' })
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)
    setFieldErrors({})

    try {
      await signUp(form)
      navigate('/')
    } catch (caught) {
      const apiError = ApiError.from(caught)
      if (apiError.status === 409) {
        setError('An account with this email already exists. Sign in instead.')
      } else if (apiError.status === 400) {
        // Identity's own rules (#43), shown next to the field each one is about.
        setFieldErrors(apiError.formErrors)
        setError('Some details need fixing.')
      } else {
        setError('Creating the account failed. Try again in a moment.')
      }
    } finally {
      setBusy(false)
    }
  }

  return (
    <Card className="mx-auto max-w-md">
      <CardHeader>
        <CardTitle className="text-xl">Create an account</CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={submit} className="flex flex-col gap-4">
          {FIELDS.map((field) => (
            <div key={field.name} className="grid gap-1.5">
              <Label htmlFor={field.name}>{field.label}</Label>
              <Input
                id={field.name}
                type={field.type}
                autoComplete={field.autoComplete}
                required
                value={form[field.name]}
                onChange={(event) => setForm({ ...form, [field.name]: event.target.value })}
              />
              {fieldErrors[field.name] && <span className="text-destructive text-xs">{fieldErrors[field.name]}</span>}
            </div>
          ))}
          {error && <ErrorMessage>{error}</ErrorMessage>}
          <Button type="submit" disabled={busy}>
            {busy ? 'Creating…' : 'Create account'}
          </Button>
        </form>
        <p className="text-muted-foreground mt-4 text-sm">
          Already have one?{' '}
          <Link to="/sign-in" className="underline">
            Sign in
          </Link>
          .
        </p>
      </CardContent>
    </Card>
  )
}
