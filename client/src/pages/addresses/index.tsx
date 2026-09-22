import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { ApiError } from '@/config/axios'
import { AddressCard } from '@/components/address/address-card'
import { AddressForm } from '@/components/address/address-form'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { useAddresses, useDeleteAddress, useMakeAddressDefault, useSaveAddress } from '@/hooks/address'
import { emptyAddress } from '@/services/address/types'

/**
 * The address book (#37). Identity keeps it and decides which one is the default; this page only asks,
 * and shows Identity's own validation messages.
 */
export function AddressesPage() {
  const { t } = useTranslation('auth')
  const { data: addresses, isPending, isError } = useAddresses()
  const save = useSaveAddress()
  const remove = useDeleteAddress()
  const makeDefault = useMakeAddressDefault()

  /** null = no form open; '' = adding a new one; otherwise the id being edited. */
  const [editing, setEditing] = useState<string | null>(null)

  const failed = (error: unknown) => toast.error(ApiError.from(error).message)

  if (isError) {
    return <ErrorMessage>{t('addresses.loadFailed')}</ErrorMessage>
  }

  if (isPending || !addresses) {
    return <LoadingRows />
  }

  const current = editing ? addresses.find((address) => address.id === editing) : undefined

  return (
    <section>
      <h1 className="mb-4 text-2xl font-bold">{t('addresses.title')}</h1>

      {addresses.length === 0 && editing === null && (
        <p className="text-muted-foreground mb-4">{t('addresses.none')}</p>
      )}

      <ul className="mb-4 grid gap-3">
        {addresses.map((address) => (
          <li key={address.id}>
            <AddressCard
              address={address}
              onEdit={() => setEditing(address.id)}
              onMakeDefault={() => makeDefault.mutate(address.id, { onError: failed })}
              onDelete={() => {
                if (confirm(t('addresses.confirmDelete', { name: address.recipientName }))) {
                  remove.mutate(address.id, { onError: failed })
                }
              }}
            />
          </li>
        ))}
      </ul>

      {editing === null ? (
        <Button onClick={() => setEditing('')}>{t('addresses.add')}</Button>
      ) : (
        <AddressForm
          key={editing}
          initial={current ?? emptyAddress}
          title={current ? t('addresses.edit') : t('addresses.new')}
          onCancel={() => setEditing(null)}
          onSave={async (fields) => {
            await save.mutateAsync({ id: current?.id, fields })
            setEditing(null)
          }}
        />
      )}
    </section>
  )
}
