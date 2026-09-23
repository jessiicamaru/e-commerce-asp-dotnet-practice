import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { AddressForm } from '@/components/address/address-form'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog'
import { useSaveAddress } from '@/hooks/address'
import { emptyAddress, type Address } from '@/services/address/types'

/**
 * Adding or editing an address without leaving the page you are on.
 *
 * <p>
 * The reason it is a dialog: at checkout, "you need an address" used to be a sentence and a link to
 * another page, and coming back meant finding your way to checkout again. An address is a short form
 * somebody fills in and returns from, which is exactly what a dialog is for - and it is used for
 * nothing longer than that.
 * </p>
 * <p>
 * `onSaved` receives the address Identity stored, so checkout can choose the one just added.
 * </p>
 */
export function AddressDialog({
  trigger,
  address,
  onSaved,
}: {
  /** What opens it - a Button element, passed through base-ui's `render`. */
  trigger: React.ReactElement
  /** Given: edit this one. Absent: add a new one. */
  address?: Address
  onSaved?: (address: Address) => void
}) {
  const { t } = useTranslation('auth')
  const save = useSaveAddress()
  const [open, setOpen] = useState(false)

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger render={trigger} />
      <DialogContent className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>{address ? t('addresses.edit') : t('addresses.new')}</DialogTitle>
          <DialogDescription>{t('addresses.dialogHint')}</DialogDescription>
        </DialogHeader>
        {/* Mounted only while open, so opening it again starts from the address, not the last half-typed draft. */}
        {open && (
          <AddressForm
            initial={address ?? emptyAddress}
            onCancel={() => setOpen(false)}
            onSave={async (fields) => {
              const saved = (await save.mutateAsync({ id: address?.id, fields })) as Address
              toast.success(t('addresses.saved'))
              setOpen(false)
              onSaved?.(saved)
            }}
          />
        )}
      </DialogContent>
    </Dialog>
  )
}
