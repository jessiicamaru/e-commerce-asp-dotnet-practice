import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { PencilIcon } from 'lucide-react'
import { toast } from 'sonner'
import { ServerError } from '@/components/shared/server-error'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useRenameShop } from '@/hooks/seller'

/**
 * Renaming the shop, in a dialog: one field, done in a moment, and it used to be a whole card that
 * sat on top of every visit to the shop page for something done once a year.
 *
 * The dialog says the surprising part out loud - one row changes the name under every listing.
 */
export function RenameShopDialog({ current }: { current: string }) {
  const { t } = useTranslation('seller')
  const rename = useRenameShop()
  const [open, setOpen] = useState(false)
  const [name, setName] = useState(current)

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) {
          setName(current)
          rename.reset()
        }
      }}
    >
      <DialogTrigger render={<Button variant="outline" size="sm" className="h-9 rounded-full px-3" />}>
        <PencilIcon /> {t('rename')}
      </DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <form
          className="grid gap-4"
          onSubmit={(event) => {
            event.preventDefault()
            const wanted = name.trim()
            if (!wanted || wanted === current) return
            rename.mutate(wanted, {
              onSuccess: () => {
                toast.success(t('renamed', { name: wanted }))
                setOpen(false)
              },
            })
          }}
        >
          <DialogHeader>
            <DialogTitle>{t('shopName')}</DialogTitle>
            <DialogDescription>{t('renameHint')}</DialogDescription>
          </DialogHeader>
          <div className="grid gap-2">
            <Label htmlFor="shopName">{t('shopName')}</Label>
            <Input id="shopName" autoFocus value={name} onChange={(event) => setName(event.target.value)} />
          </div>
          <ServerError error={rename.error} fallback={t('listing.loadFailed')} />
          <DialogFooter>
            <DialogClose render={<Button type="button" variant="ghost" />}>
              {t('action.cancel', { ns: 'common' })}
            </DialogClose>
            <Button type="submit" disabled={rename.isPending || !name.trim() || name.trim() === current}>
              {rename.isPending ? t('action.saving', { ns: 'common' }) : t('action.save', { ns: 'common' })}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
