import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { PencilIcon } from 'lucide-react'
import { toast } from 'sonner'
import { ServerError } from '@/components/shared/server-error'
import { Button } from '@ecommerce/ui/button'
import { Checkbox } from '@ecommerce/ui/checkbox'
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
import { Input } from '@ecommerce/ui/input'
import { Label } from '@ecommerce/ui/label'
import { useEditVoucher } from '@ecommerce/core/hooks/voucher'
import type { VoucherSummary } from '@ecommerce/core/services/voucher/types'
import { editFields, toVoucherEdit, type EditFields } from './to-voucher-edit'

/**
 * "Edit" on an active voucher (specs/113): its name, end, limits and minimums, prefilled - never what it takes off,
 * which the dialog says rather than offering a box that would be refused.
 */
export function VoucherEdit({ voucher }: { voucher: VoucherSummary }) {
  const { t } = useTranslation('vouchers')
  const edit = useEditVoucher()
  const [open, setOpen] = useState(false)
  const [fields, setFields] = useState<EditFields>(() => editFields(voucher))
  const hasMinQuantity = voucher.conditions.some((c) => c.type === 'MinQuantity')

  const openWith = (next: boolean) => {
    if (next) {
      setFields(editFields(voucher))   // what the voucher says now, not what was typed and abandoned last time
      edit.reset()
    }
    setOpen(next)
  }

  const save = (event: FormEvent) => {
    event.preventDefault()
    // mutateAsync: the list re-reads and this row may re-render before a callback to mutate() would run (specs/080).
    void edit.mutateAsync({ id: voucher.id, edit: toVoucherEdit(fields, voucher) }).then(
      () => {
        toast.success(t('edited'))
        setOpen(false)
      },
      () => undefined,
    )
  }

  const set = (patch: Partial<EditFields>) => setFields((current) => ({ ...current, ...patch }))

  return (
    <Dialog open={open} onOpenChange={openWith}>
      <DialogTrigger render={<Button variant="outline" size="sm" className="rounded-full" />}>
        <PencilIcon /> {t('edit')}
      </DialogTrigger>
      <DialogContent>
        <form onSubmit={save} className="grid gap-4">
          <DialogHeader>
            <DialogTitle>{t('editTitle', { code: voucher.code })}</DialogTitle>
            <DialogDescription>{t('editBody')}</DialogDescription>
          </DialogHeader>
          <Field id="edit-name" label={t('form.name')}>
            <Input id="edit-name" required maxLength={100} value={fields.name} onChange={(e) => set({ name: e.target.value })} />
          </Field>
          <Field id="edit-ends" label={t('form.endsAt')}>
            <Input id="edit-ends" type="datetime-local" value={fields.endsAt} onChange={(e) => set({ endsAt: e.target.value })} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field id="edit-total" label={t('form.totalLimit')}>
              <Input id="edit-total" type="number" min={Math.max(1, voucher.usedCount)} placeholder={t('form.noLimit')} value={fields.totalLimit} onChange={(e) => set({ totalLimit: e.target.value })} />
            </Field>
            <Field id="edit-per" label={t('form.perCustomerLimit')}>
              <Input id="edit-per" type="number" min={1} placeholder={t('form.noLimit')} value={fields.perCustomerLimit} onChange={(e) => set({ perCustomerLimit: e.target.value })} />
            </Field>
          </div>
          {voucher.amounts.map((amount) => (
            <Field key={amount.currency} id={`edit-min-${amount.currency}`} label={`${t('form.minSubtotal')} (${amount.currency})`}>
              <Input
                id={`edit-min-${amount.currency}`}
                type="number"
                min={0}
                step={amount.currency === 'VND' ? 1 : 0.01}
                value={fields.minSubtotals[amount.currency] ?? ''}
                onChange={(e) => set({ minSubtotals: { ...fields.minSubtotals, [amount.currency]: e.target.value } })}
              />
            </Field>
          ))}
          {hasMinQuantity && (
            <Field id="edit-quantity" label={t('form.minQuantity')}>
              <Input id="edit-quantity" type="number" min={1} max={1000} value={fields.minQuantity} onChange={(e) => set({ minQuantity: e.target.value })} />
            </Field>
          )}
          <label className="flex items-center gap-2 text-sm">
            <Checkbox checked={fields.isPublic} onCheckedChange={(checked) => set({ isPublic: checked })} /> {t('public.show')}
          </label>
          <ServerError error={edit.error} fallback={t('editFailed')} />
          <DialogFooter>
            <DialogClose render={<Button type="button" variant="outline" />}>{t('action.cancel', { ns: 'common' })}</DialogClose>
            <Button type="submit" disabled={edit.isPending || !fields.name.trim()}>
              {edit.isPending ? t('saving') : t('save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function Field({ id, label, children }: { id: string; label: string; children: React.ReactNode }) {
  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      {children}
    </div>
  )
}
