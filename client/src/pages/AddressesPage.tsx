import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { ApiError } from '../api/http'
import {
  createAddress,
  deleteAddress,
  describe,
  emptyAddress,
  listAddresses,
  makeDefault,
  updateAddress,
  type Address,
  type AddressFields,
} from '../api/addresses'

// The address book (#37). Identity keeps it and decides which one is the default; this page only
// asks. Validation is Identity's too - its messages are shown next to the field they belong to.
export function AddressesPage() {
  const [addresses, setAddresses] = useState<Address[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  /** null = no form open; '' = adding a new one; otherwise the id being edited. */
  const [editing, setEditing] = useState<string | null>(null)

  const reload = useCallback(
    () =>
      listAddresses()
        .then((a) => {
          setAddresses(a)
          setError(null)
        })
        .catch(() => setError('Your addresses could not be loaded.')),
    [],
  )

  useEffect(() => {
    void reload()
  }, [reload])

  async function act(action: () => Promise<void>) {
    try {
      await action()
      await reload()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'That change could not be saved.')
    }
  }

  if (!addresses) return error ? <p className="error">{error}</p> : <p className="muted">Loading…</p>

  const current = editing ? addresses.find((a) => a.id === editing) : undefined

  return (
    <section>
      <h1>Delivery addresses</h1>
      {error && <p className="error">{error}</p>}

      {addresses.length === 0 && editing === null && <p className="muted">You have no saved addresses yet.</p>}

      <ul className="addresses">
        {addresses.map((a) => (
          <li key={a.id} className="card">
            <strong>
              {a.recipientName} {a.isDefault && <span className="badge">Default</span>}
            </strong>
            <span>{describe(a)}</span>
            {a.phone && <span className="muted small">{a.phone}</span>}
            <span className="actions">
              <button className="link" onClick={() => setEditing(a.id)}>
                Edit
              </button>
              {!a.isDefault && (
                <button className="link" onClick={() => void act(() => makeDefault(a.id))}>
                  Make default
                </button>
              )}
              <button
                className="link"
                onClick={() => {
                  if (confirm(`Delete the address for ${a.recipientName}?`)) void act(() => deleteAddress(a.id))
                }}
              >
                Delete
              </button>
            </span>
          </li>
        ))}
      </ul>

      {editing === null ? (
        <button onClick={() => setEditing('')}>Add an address</button>
      ) : (
        <AddressForm
          key={editing}
          initial={current ?? emptyAddress}
          title={current ? 'Edit address' : 'New address'}
          onCancel={() => setEditing(null)}
          onSave={async (fields) => {
            if (current) await updateAddress(current.id, fields)
            else await createAddress(fields)
            setEditing(null)
            await reload()
          }}
        />
      )}
    </section>
  )
}

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

function AddressForm({
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

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    setFieldErrors({})
    try {
      // Optional fields go as null rather than "", so an emptied Line 2 is cleared, not stored blank.
      const cleaned = Object.fromEntries(
        Object.entries(fields).map(([k, v]) => [k, typeof v === 'string' && v.trim() === '' ? null : v]),
      ) as unknown as AddressFields
      await onSave({ ...cleaned, country: (cleaned.country ?? '').toUpperCase() })
    } catch (err) {
      if (err instanceof ApiError && err.status === 400 && err.problem.errors) {
        // Identity names fields in PascalCase (PostalCode); the form uses camelCase.
        const byField: Record<string, string> = {}
        for (const [k, v] of Object.entries(err.fieldErrors)) byField[k.charAt(0).toLowerCase() + k.slice(1)] = v
        setFieldErrors(byField)
      } else {
        setError(err instanceof ApiError ? err.message : 'The address could not be saved.')
      }
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} className="form narrow">
      <h2>{title}</h2>
      {FIELDS.map((f) => (
        <label key={f.name}>
          {f.label}
          {f.hint && <span className="muted small">{f.hint}</span>}
          <input
            required={f.required}
            maxLength={f.name === 'country' ? 2 : undefined}
            value={fields[f.name] ?? ''}
            onChange={(e) => setFields({ ...fields, [f.name]: e.target.value })}
          />
          {fieldErrors[f.name] && <span className="error">{fieldErrors[f.name]}</span>}
        </label>
      ))}
      {error && <p className="error" role="alert">{error}</p>}
      <div className="actions">
        <button disabled={busy}>{busy ? 'Saving…' : 'Save'}</button>
        <button type="button" className="link" onClick={onCancel}>
          Cancel
        </button>
      </div>
    </form>
  )
}
