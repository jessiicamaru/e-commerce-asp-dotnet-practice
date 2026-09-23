import { useTranslation } from 'react-i18next'
import { MapPinIcon, PlusIcon } from 'lucide-react'
import { toast } from 'sonner'
import { ApiError } from '@/config/axios'
import { AddressCard } from '@/components/address/address-card'
import { AddressDialog } from '@/components/address/address-dialog'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { useAddresses, useDeleteAddress, useMakeAddressDefault } from '@/hooks/address'

/**
 * The address book (#37). Identity keeps it and decides which one is the default; this page only asks,
 * and shows Identity's own validation messages.
 */
export function AddressesPage() {
  const { t } = useTranslation('auth')
  const { data: addresses, isPending, isError } = useAddresses()
  const remove = useDeleteAddress()
  const makeDefault = useMakeAddressDefault()

  const failed = (error: unknown) => toast.error(ApiError.from(error).message)

  if (isError) {
    return <ErrorMessage>{t('addresses.loadFailed')}</ErrorMessage>
  }

  if (isPending || !addresses) {
    return <LoadingRows />
  }

  const add = (
    <AddressDialog
      trigger={
        <Button className="h-10 rounded-full px-4 font-semibold">
          <PlusIcon /> {t('addresses.add')}
        </Button>
      }
    />
  )

  return (
    <section className="grid gap-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div className="grid gap-1">
          <h1 className="text-2xl font-bold tracking-tight">{t('addresses.title')}</h1>
          <p className="text-muted-foreground text-sm">{t('addresses.subtitle')}</p>
        </div>
        {addresses.length > 0 && add}
      </header>

      {addresses.length === 0 ? (
        <div className="bg-card ring-border/60 mx-auto grid max-w-md justify-items-center gap-3 rounded-3xl p-10 text-center ring-1">
          <span className="bg-accent text-accent-foreground grid size-14 place-items-center rounded-2xl">
            <MapPinIcon className="size-7" />
          </span>
          <h2 className="text-lg font-semibold">{t('addresses.none')}</h2>
          <p className="text-muted-foreground text-sm">{t('addresses.noneHint')}</p>
          {add}
        </div>
      ) : (
        <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {addresses.map((address) => (
            <li key={address.id}>
              <AddressCard
                address={address}
                onMakeDefault={() => makeDefault.mutate(address.id, { onError: failed })}
                onDelete={() => remove.mutate(address.id, { onError: failed })}
              />
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
