import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { PlusIcon, XIcon } from 'lucide-react'
import { toast } from 'sonner'
import { ServerError } from '@/components/shared/server-error'
import { ProductPicker } from '@/components/voucher/product-picker'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
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
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { CURRENCIES } from '@/config/money'
import { useCreateVoucher } from '@/hooks/voucher'
import type { Benefit } from '@/services/voucher/types'
import { emptyRow, toNewVoucher, type AmountRow } from './to-new-voucher'

/** The "new voucher" button and its dialog - the platform's for an administrator, a shop's for a seller. */
export function VoucherForm({ platform }: { platform: boolean }) {
  const { t } = useTranslation('vouchers')
  const create = useCreateVoucher()
  const [open, setOpen] = useState(false)
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [benefit, setBenefit] = useState<Benefit>('Percent')
  const [percent, setPercent] = useState('10')
  const [rows, setRows] = useState<AmountRow[]>([emptyRow(CURRENCIES[0])])
  const [startsAt, setStartsAt] = useState('')
  const [endsAt, setEndsAt] = useState('')
  const [totalLimit, setTotalLimit] = useState('')
  const [perCustomerLimit, setPerCustomerLimit] = useState('')
  const [newCustomer, setNewCustomer] = useState(false)
  const [firstInShop, setFirstInShop] = useState(false)
  const [minQuantity, setMinQuantity] = useState('')
  const [products, setProducts] = useState<Map<string, string>>(new Map())
  const benefits: Benefit[] = platform ? ['Percent', 'FixedAmount', 'FreeShipping'] : ['Percent', 'FixedAmount']
  const unused = CURRENCIES.filter((c) => !rows.some((r) => r.currency === c))

  const setRow = (index: number, patch: Partial<AmountRow>) =>
    setRows(rows.map((row, i) => (i === index ? { ...row, ...patch } : row)))

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) create.reset()
      }}
    >
      <DialogTrigger render={<Button className="h-10 justify-self-start rounded-full px-4 font-semibold" />}>
        <PlusIcon /> {t('new')}
      </DialogTrigger>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-lg">
        <form
          className="grid gap-4"
          onSubmit={(event) => {
            event.preventDefault()
            const voucher = toNewVoucher({
              platform, code, name, benefit, percent, rows, startsAt, endsAt, totalLimit, perCustomerLimit,
              newCustomer, firstInShop, minQuantity, products: [...products.keys()],
            })
            create.mutate(voucher, {
              onSuccess: (made) => {
                toast.success(t('created', { code: made.code }))
                setOpen(false)
              },
            })
          }}
        >
          <DialogHeader>
            <DialogTitle>{t('form.title')}</DialogTitle>
            <DialogDescription>{t('form.body')}</DialogDescription>
          </DialogHeader>

          <div className="grid gap-3 sm:grid-cols-2">
            <Field id="voucher-code" label={t('form.code')} hint={t('form.codeHint')}>
              <Input id="voucher-code" required maxLength={32} className="font-mono uppercase" value={code} onChange={(e) => setCode(e.target.value)} />
            </Field>
            <Field id="voucher-name" label={t('form.name')}>
              <Input id="voucher-name" required maxLength={100} value={name} onChange={(e) => setName(e.target.value)} />
            </Field>
          </div>

          <fieldset className="grid gap-2">
            <legend className="mb-1 text-sm font-medium">{t('form.benefit')}</legend>
            <RadioGroup value={benefit} onValueChange={(value) => setBenefit(value as Benefit)} className="gap-1.5">
              {benefits.map((b) => (
                <label key={b} className="flex items-center gap-2 text-sm">
                  <RadioGroupItem value={b} /> {t(`form.benefits.${b}`)}
                </label>
              ))}
            </RadioGroup>
            {benefit === 'Percent' && (
              <Field id="voucher-percent" label={t('form.percent')}>
                <Input id="voucher-percent" type="number" min={1} max={100} step="0.01" required value={percent} onChange={(e) => setPercent(e.target.value)} />
              </Field>
            )}
          </fieldset>

          <fieldset className="grid gap-2">
            <legend className="text-sm font-medium">{t('form.amounts')}</legend>
            <p className="text-muted-foreground text-xs">{t('form.amountsHint')}</p>
            {rows.map((row, index) => (
              <div key={row.currency} className="ring-border/60 grid grid-cols-2 gap-2 rounded-xl p-2 ring-1 sm:grid-cols-[4rem_1fr_1fr_auto]">
                <span className="self-center font-mono text-sm font-semibold">{row.currency}</span>
                {benefit === 'FixedAmount' ? (
                  <AmountInput label={t('form.fixedValue')} currency={row.currency} required value={row.fixedValue} onChange={(v) => setRow(index, { fixedValue: v })} />
                ) : (
                  <AmountInput label={t('form.maxDiscount')} currency={row.currency} value={row.maxDiscount} onChange={(v) => setRow(index, { maxDiscount: v })} />
                )}
                <AmountInput label={t('form.minSubtotal')} currency={row.currency} value={row.minSubtotal} onChange={(v) => setRow(index, { minSubtotal: v })} />
                {rows.length > 1 && (
                  <Button type="button" variant="ghost" size="icon" aria-label={t('form.removeCurrency', { currency: row.currency })} onClick={() => setRows(rows.filter((_, i) => i !== index))}>
                    <XIcon />
                  </Button>
                )}
              </div>
            ))}
            {unused.length > 0 && (
              <Button type="button" variant="outline" size="sm" className="justify-self-start rounded-full" onClick={() => setRows([...rows, emptyRow(unused[0])])}>
                <PlusIcon /> {t('form.addCurrency')} ({unused[0]})
              </Button>
            )}
          </fieldset>

          <div className="grid gap-3 sm:grid-cols-2">
            <Field id="voucher-starts" label={t('form.startsAt')}>
              <Input id="voucher-starts" type="datetime-local" value={startsAt} onChange={(e) => setStartsAt(e.target.value)} />
            </Field>
            <Field id="voucher-ends" label={t('form.endsAt')}>
              <Input id="voucher-ends" type="datetime-local" value={endsAt} onChange={(e) => setEndsAt(e.target.value)} />
            </Field>
            <Field id="voucher-total" label={t('form.totalLimit')}>
              <Input id="voucher-total" type="number" min={1} placeholder={t('form.noLimit')} value={totalLimit} onChange={(e) => setTotalLimit(e.target.value)} />
            </Field>
            <Field id="voucher-per" label={t('form.perCustomerLimit')}>
              <Input id="voucher-per" type="number" min={1} placeholder={t('form.noLimit')} value={perCustomerLimit} onChange={(e) => setPerCustomerLimit(e.target.value)} />
            </Field>
          </div>

          <fieldset className="grid gap-2">
            <legend className="mb-1 text-sm font-medium">{t('form.conditions')}</legend>
            {platform ? (
              <label className="flex items-center gap-2 text-sm">
                <Checkbox checked={newCustomer} onCheckedChange={setNewCustomer} /> {t('form.newCustomer')}
              </label>
            ) : (
              <label className="flex items-center gap-2 text-sm">
                <Checkbox checked={firstInShop} onCheckedChange={setFirstInShop} /> {t('form.firstInShop')}
              </label>
            )}
            <Field id="voucher-quantity" label={t('form.minQuantity')}>
              <Input id="voucher-quantity" type="number" min={1} max={1000} placeholder={t('form.noLimit')} value={minQuantity} onChange={(e) => setMinQuantity(e.target.value)} />
            </Field>
          </fieldset>

          {benefit !== 'FreeShipping' && (
            <fieldset className="grid gap-2">
              <legend className="text-sm font-medium">{t('form.targets')}</legend>
              <p className="text-muted-foreground text-xs">{t('form.targetsHint')}</p>
              <ProductPicker mine={!platform} chosen={products} onChange={setProducts} />
            </fieldset>
          )}

          <ServerError error={create.error} fallback={t('form.failed')} />
          <DialogFooter>
            <DialogClose render={<Button type="button" variant="ghost" />}>{t('action.cancel', { ns: 'common' })}</DialogClose>
            <Button type="submit" disabled={create.isPending || !code.trim() || !name.trim()}>
              {t('form.create')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function Field({ id, label, hint, children }: { id: string; label: string; hint?: string; children: React.ReactNode }) {
  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      {children}
      {hint && <p className="text-muted-foreground text-xs">{hint}</p>}
    </div>
  )
}

/** An amount in one currency: whole numbers for dong, cents for dollars - what the currency can hold (specs/022). */
function AmountInput({ label, currency, value, onChange, required }: { label: string; currency: string; value: string; onChange: (value: string) => void; required?: boolean }) {
  return (
    <Input
      aria-label={`${label} (${currency})`}
      placeholder={label}
      type="number"
      min={0}
      step={currency === 'VND' ? 1 : 0.01}
      required={required}
      value={value}
      onChange={(event) => onChange(event.target.value)}
    />
  )
}
