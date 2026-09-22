import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '@/config/axios'
import { ErrorMessage } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { AddressFields } from '@/services/address/types'
import { normaliseAddress } from '@/utils/address'

const FIELDS: { name: keyof AddressFields; required?: boolean; hint?: boolean }[] = [
  { name: 'recipientName', required: true },
  { name: 'line1', required: true },
  { name: 'line2' },
  { name: 'city', required: true },
  { name: 'region' },
  { name: 'postalCode', required: true },
  { name: 'country', required: true, hint: true },
  { name: 'phone' },
]

/**
 * Add or edit an address. Validation is Identity's: its per-field messages are shown next to the field
 * they belong to, so the form never has to repeat the rules.
 */
export function AddressForm({
  initial,
  title,
  onSave,
  onCancel,
}: {
  initial: AddressFields
  title: string
  onSave: (fields: AddressFields) => Promise<void>
  onCancel: () => void
}) {
  const { t } = useTranslation('auth')
  const [fields, setFields] = useState<AddressFields>(initial)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)
    setFieldErrors({})

    try {
      await onSave(normaliseAddress(fields))
    } catch (caught) {
      const apiError = ApiError.from(caught)
      if (apiError.status === 400 && apiError.problem.errors) {
        setFieldErrors(apiError.formErrors)
      } else {
        setError(apiError.message)
      }
    } finally {
      setBusy(false)
    }
  }

  return (
    <Card className="max-w-md">
      <CardHeader>
        <CardTitle>{title}</CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={submit} className="flex flex-col gap-4">
          {FIELDS.map((field) => (
            <div key={field.name} className="grid gap-1.5">
              <Label htmlFor={field.name}>{t(`addresses.fields.${field.name}`)}</Label>
              {field.hint && <span className="text-muted-foreground text-xs">{t('addresses.countryHint')}</span>}
              <Input
                id={field.name}
                required={field.required}
                maxLength={field.name === 'country' ? 2 : undefined}
                value={fields[field.name] ?? ''}
                onChange={(event) => setFields({ ...fields, [field.name]: event.target.value })}
              />
              {fieldErrors[field.name] && <span className="text-destructive text-xs">{fieldErrors[field.name]}</span>}
            </div>
          ))}
          {error && <ErrorMessage>{error}</ErrorMessage>}
          <div className="flex items-center gap-3">
            <Button type="submit" disabled={busy}>
              {busy ? t('action.saving', { ns: 'common' }) : t('action.save', { ns: 'common' })}
            </Button>
            <Button type="button" variant="ghost" onClick={onCancel}>
              {t('action.cancel', { ns: 'common' })}
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  )
}
