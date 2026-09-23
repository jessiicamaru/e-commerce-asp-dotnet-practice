import { useMemo, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '@/config/axios'
import { ErrorMessage } from '@/components/shared/query-state'
import { SearchableSelect } from '@/components/shared/searchable-select'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { AddressFields } from '@/services/address/types'
import { normaliseAddress } from '@/utils/address'
import { countryChoices } from '@/utils/address/countries'
import { cn } from '@/utils/shared'

/** The fields, in the order a person writes an address, and which of them take a whole row. */
const FIELDS: { name: Exclude<keyof AddressFields, 'country'>; required?: boolean; wide?: boolean; type?: string }[] = [
  { name: 'recipientName', required: true },
  { name: 'phone', type: 'tel' },
  { name: 'line1', required: true, wide: true },
  { name: 'line2', wide: true },
  { name: 'city', required: true },
  { name: 'region' },
  { name: 'postalCode', required: true },
]

/**
 * Add or edit an address. Validation is Identity's: its per-field messages are shown next to the field
 * they belong to, so the form never has to repeat the rules.
 *
 * The country is chosen by name from a searchable list, and the two-letter code it stands for is what
 * is sent - the code decides the tax rate at checkout (ADR-002), so it must be exact.
 */
export function AddressForm({
  initial,
  onSave,
  onCancel,
}: {
  initial: AddressFields
  onSave: (fields: AddressFields) => Promise<void>
  onCancel: () => void
}) {
  const { t, i18n } = useTranslation('auth')
  const [fields, setFields] = useState<AddressFields>({ ...initial, country: initial.country || 'VN' })
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const countries = useMemo(() => countryChoices(i18n.language), [i18n.language])

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
    <form onSubmit={submit} className="grid gap-4">
      <div className="grid gap-4 sm:grid-cols-2">
        {FIELDS.map((field) => (
          <div key={field.name} className={cn('grid gap-1.5', field.wide && 'sm:col-span-2')}>
            <Label htmlFor={field.name}>
              {t(`addresses.fields.${field.name}`)}
              {!field.required && <span className="text-muted-foreground font-normal"> ({t('addresses.optional')})</span>}
            </Label>
            <Input
              id={field.name}
              type={field.type}
              required={field.required}
              className="h-10 rounded-xl"
              value={fields[field.name] ?? ''}
              onChange={(event) => setFields({ ...fields, [field.name]: event.target.value })}
            />
            {fieldErrors[field.name] && <span className="text-destructive text-xs">{fieldErrors[field.name]}</span>}
          </div>
        ))}

        <div className="grid gap-1.5">
          <Label htmlFor="country">{t('addresses.fields.country')}</Label>
          <SearchableSelect
            id="country"
            required
            choices={countries}
            value={fields.country || null}
            onChange={(value) => setFields({ ...fields, country: value ?? '' })}
          />
          {fieldErrors.country && <span className="text-destructive text-xs">{fieldErrors.country}</span>}
        </div>
      </div>

      {error && <ErrorMessage>{error}</ErrorMessage>}

      <div className="flex justify-end gap-2">
        <Button type="button" variant="ghost" className="rounded-full" onClick={onCancel}>
          {t('action.cancel', { ns: 'common' })}
        </Button>
        <Button type="submit" className="rounded-full px-5" disabled={busy}>
          {busy ? t('action.saving', { ns: 'common' }) : t('action.save', { ns: 'common' })}
        </Button>
      </div>
    </form>
  )
}
