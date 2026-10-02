import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { TextIcon } from 'lucide-react'
import { ServerError } from '@ecommerce/core/components/shared/server-error'
import { Button } from '@ecommerce/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@ecommerce/ui/dialog'
import { Label } from '@ecommerce/ui/label'
import { Textarea } from '@ecommerce/ui/textarea'
import { useDescribeShop } from '@ecommerce/core/hooks/seller'

/**
 * The shop's description, beside its name (specs/099): a few words shoppers read on the shop's page. Empty clears it.
 * `mutateAsync().then` rather than a callback to `mutate`, so the toast survives the dialog closing (specs/080).
 */
export function DescribeShopDialog({ current }: { current: string }) {
  const { t } = useTranslation('seller')
  const describe = useDescribeShop()
  const [open, setOpen] = useState(false)
  const [text, setText] = useState(current)

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) {
          setText(current)
          describe.reset()
        }
      }}
    >
      <DialogTrigger render={<Button variant="outline" size="sm" className="h-9 rounded-full px-3" />}>
        <TextIcon /> {t('describe')}
      </DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <form
          className="grid gap-4"
          onSubmit={(event) => {
            event.preventDefault()
            describe.mutateAsync(text.trim() || null).then(() => {
              toast.success(t('described'))
              setOpen(false)
            }, () => {})
          }}
        >
          <DialogHeader>
            <DialogTitle>{t('description')}</DialogTitle>
            <DialogDescription>{t('describeHint')}</DialogDescription>
          </DialogHeader>
          <div className="grid gap-2">
            <Label htmlFor="shopDescription">{t('description')}</Label>
            <Textarea id="shopDescription" value={text} maxLength={500} rows={4} onChange={(event) => setText(event.target.value)} />
          </div>
          <ServerError error={describe.error} fallback={t('listing.loadFailed')} />
          <DialogFooter>
            <DialogClose render={<Button type="button" variant="ghost" />}>{t('action.cancel', { ns: 'common' })}</DialogClose>
            <Button type="submit" disabled={describe.isPending || text.trim() === current.trim()}>
              {describe.isPending ? t('action.saving', { ns: 'common' }) : t('action.save', { ns: 'common' })}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
