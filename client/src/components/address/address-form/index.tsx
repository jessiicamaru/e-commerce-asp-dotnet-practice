import { useState, type FormEvent } from 'react'
import { ApiError } from '@/config/axios'
import { ErrorMessage } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { AddressFields } from '@/services/address/types'
import { normaliseAddress } from '@/utils/address'

const FIELDS: { name: keyof AddressFields; label: string; required?: boolean; hint?: string }[] = [
  { name: 'recipientName', label: 'Recipient name', required: true },
  { name: 'line1', label: 'Address line 1', required: true },
  { name: 'line2', label: 'Address line 2' },
  { name: 'city', label: 'City', required: true },
  { name: 'region', label: 'Region / state' },
  { name: 'postalCode', label: 'Postal code', required: true },
  { name: 'country', label: 'Country', required: true, hint: 'Two-letter code, such as VN or GB' },
  { name: 'phone', label: 'Phone' },
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
              <Label htmlFor={field.name}>{field.label}</Label>
              {field.hint && <span className="text-muted-foreground text-xs">{field.hint}</span>}
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
              {busy ? 'Saving…' : 'Save'}
            </Button>
            <Button type="button" variant="ghost" onClick={onCancel}>
              Cancel
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  )
}
