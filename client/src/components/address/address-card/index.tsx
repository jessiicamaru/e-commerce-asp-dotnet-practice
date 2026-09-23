import { useTranslation } from 'react-i18next'
import { MapPinIcon, PencilIcon, PhoneIcon, StarIcon, Trash2Icon } from 'lucide-react'
import { AddressDialog } from '@/components/address/address-dialog'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardFooter } from '@/components/ui/card'
import type { Address } from '@/services/address/types'
import { describeAddress } from '@/utils/address'

/** One saved address and what can be done to it: edit (a dialog), make default, delete (confirmed). */
export function AddressCard({
  address,
  onMakeDefault,
  onDelete,
}: {
  address: Address
  onMakeDefault: () => void
  onDelete: () => void
}) {
  const { t } = useTranslation('auth')

  return (
    <Card className="h-full rounded-3xl">
      <CardContent className="grid gap-2">
        <div className="flex items-start justify-between gap-2">
          <span className="flex items-center gap-2 font-semibold">
            <MapPinIcon className="text-muted-foreground size-4" />
            {address.recipientName}
          </span>
          {address.isDefault && <Badge>{t('addresses.default')}</Badge>}
        </div>
        <p className="text-sm">{describeAddress(address)}</p>
        {address.phone && (
          <p className="text-muted-foreground flex items-center gap-2 text-xs">
            <PhoneIcon className="size-3.5" /> {address.phone}
          </p>
        )}
      </CardContent>
      <CardFooter className="mt-auto flex flex-wrap gap-1">
        <AddressDialog
          address={address}
          trigger={
            <Button variant="ghost" size="sm" className="rounded-full">
              <PencilIcon /> {t('action.edit', { ns: 'common' })}
            </Button>
          }
        />
        {!address.isDefault && (
          <Button variant="ghost" size="sm" className="rounded-full" onClick={onMakeDefault}>
            <StarIcon /> {t('addresses.makeDefault')}
          </Button>
        )}
        <AlertDialog>
          <AlertDialogTrigger
            render={<Button variant="ghost" size="sm" className="text-muted-foreground hover:text-destructive ml-auto rounded-full" />}
          >
            <Trash2Icon /> {t('action.delete', { ns: 'common' })}
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>{t('addresses.deleteTitle')}</AlertDialogTitle>
              <AlertDialogDescription>
                {t('addresses.confirmDelete', { name: address.recipientName })}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>{t('action.cancel', { ns: 'common' })}</AlertDialogCancel>
              <AlertDialogAction variant="destructive" onClick={onDelete}>
                {t('action.delete', { ns: 'common' })}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </CardFooter>
    </Card>
  )
}
