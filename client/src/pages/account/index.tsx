import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { ServerError } from '@/components/shared/server-error'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ApiError } from '@/config/axios'
import { useAuth } from '@/context/auth/useAuth'
import { useChangePassword, useMe, useUpdateMe } from '@/hooks/me'
import type { AccountProfile } from '@/services/auth/types'
import { tooManyAttempts } from '@/utils/shared'

/** Your account (specs/064): your details and your password, each changed by you and nobody else. */
export function AccountPage() {
  const { t } = useTranslation('auth')
  const { user } = useAuth()

  return (
    <section className="mx-auto grid max-w-2xl gap-6">
      <div>
        <h1 className="mb-1 text-2xl font-bold">{t('account.title')}</h1>
        <p className="text-muted-foreground text-sm">{user?.email}</p>
        <p className="mt-3 flex gap-3 text-sm">
          <Link to="/addresses" className="underline">
            {t('account.addresses')}
          </Link>
          <Link to="/cart" className="underline">
            {t('account.cart')}
          </Link>
          <Link to="/orders" className="underline">
            {t('account.orders')}
          </Link>
        </p>
      </div>
      <DetailsForm />
      <PasswordForm />
    </section>
  )
}

function DetailsForm() {
  const { t } = useTranslation('auth')
  const me = useMe()

  if (me.isError) return <ErrorMessage>{t('account.loadFailed')}</ErrorMessage>
  if (me.isPending) return <LoadingRows />

  return <DetailsFields profile={me.data} />
}

/** The form starts from the details as loaded; what is typed afterwards is the form's own. */
function DetailsFields({ profile }: { profile: AccountProfile }) {
  const { t } = useTranslation('auth')
  const { refreshSession } = useAuth()
  const update = useUpdateMe()
  const [form, setForm] = useState({ firstName: profile.firstName, lastName: profile.lastName, phone: profile.phone ?? '' })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    update.mutate(form, {
      onSuccess: async () => {
        toast.success(t('account.saved'))
        // The name in the header and in the token (reviews are signed with it) come from the session.
        await refreshSession()
      },
    })
  }

  return (
    <form onSubmit={submit} className="bg-card ring-border/60 grid gap-4 rounded-3xl p-6 ring-1">
      <div>
        <h2 className="font-semibold">{t('account.details')}</h2>
        <p className="text-muted-foreground text-sm">{t('account.detailsHint')}</p>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="grid gap-1.5">
          <Label htmlFor="firstName">{t('account.firstName')}</Label>
          <Input
            id="firstName"
            required
            maxLength={100}
            value={form.firstName}
            onChange={(event) => setForm({ ...form, firstName: event.target.value })}
          />
        </div>
        <div className="grid gap-1.5">
          <Label htmlFor="lastName">{t('account.lastName')}</Label>
          <Input
            id="lastName"
            required
            maxLength={100}
            value={form.lastName}
            onChange={(event) => setForm({ ...form, lastName: event.target.value })}
          />
        </div>
      </div>
      <div className="grid gap-1.5">
        <Label htmlFor="phone">
          {t('account.phone')} <span className="text-muted-foreground text-xs">({t('account.optional')})</span>
        </Label>
        <Input
          id="phone"
          type="tel"
          maxLength={20}
          value={form.phone}
          onChange={(event) => setForm({ ...form, phone: event.target.value })}
        />
      </div>
      <ServerError error={update.error} fallback={t('account.saveFailed')} />
      <Button type="submit" className="justify-self-start rounded-full" disabled={update.isPending}>
        {update.isPending ? t('account.saving') : t('account.save')}
      </Button>
    </form>
  )
}

function PasswordForm() {
  const { t } = useTranslation('auth')
  const change = useChangePassword()
  const empty = { currentPassword: '', newPassword: '', confirm: '' }
  const [form, setForm] = useState(empty)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setError(null)
    setFieldErrors({})
    if (form.newPassword !== form.confirm) {
      setError(t('account.mismatch'))
      return
    }

    change.mutate(
      { currentPassword: form.currentPassword, newPassword: form.newPassword },
      {
        onSuccess: () => {
          toast.success(t('account.changed'))
          setForm(empty)
        },
        onError: (caught) => {
          const apiError = ApiError.from(caught)
          if (apiError.status === 400) {
            // "Your current password is not correct", or registration's rules - beside the field.
            setFieldErrors(apiError.formErrors)
          } else {
            setError(tooManyAttempts(t, caught) ?? t('account.changeFailed'))
          }
        },
      },
    )
  }

  const field = (name: 'currentPassword' | 'newPassword' | 'confirm', label: string, autoComplete: string) => (
    <div className="grid gap-1.5">
      <Label htmlFor={name}>{label}</Label>
      <Input
        id={name}
        type="password"
        required
        autoComplete={autoComplete}
        value={form[name]}
        onChange={(event) => setForm({ ...form, [name]: event.target.value })}
      />
      {fieldErrors[name] && <p className="text-destructive text-sm">{fieldErrors[name]}</p>}
    </div>
  )

  return (
    <form onSubmit={submit} className="bg-card ring-border/60 grid gap-4 rounded-3xl p-6 ring-1">
      <div>
        <h2 className="font-semibold">{t('account.password')}</h2>
        <p className="text-muted-foreground text-sm">{t('account.passwordHint')}</p>
      </div>
      {field('currentPassword', t('account.currentPassword'), 'current-password')}
      {field('newPassword', t('account.newPassword'), 'new-password')}
      {field('confirm', t('account.confirmPassword'), 'new-password')}
      {error && <ErrorMessage>{error}</ErrorMessage>}
      <Button type="submit" className="justify-self-start rounded-full" disabled={change.isPending}>
        {change.isPending ? t('account.changing') : t('account.changePassword')}
      </Button>
    </form>
  )
}
