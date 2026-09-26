import { useId, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { TicketPercentIcon, XIcon } from 'lucide-react'
import { ServerError } from '@/components/shared/server-error'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useTryVoucher } from '@/hooks/voucher'
import type { CheckoutChoice } from '@/services/order/types'

/** The most codes one order takes (specs/069); the server refuses a sixth on its own. */
export const MAX_VOUCHER_CODES = 5

/**
 * Voucher codes at checkout (specs/070). A code is tried with the server first - the quote with the codes
 * already applied plus this one - and joins the list only if the server takes it; a refusal is its words, here,
 * where the code was typed (research D1). The summary's own quote only ever sees codes that worked.
 */
export function VoucherBox({
  choice,
  codes,
  onChange,
}: {
  /** Where and how - what the code is tried against. Null until both are chosen. */
  choice: Omit<CheckoutChoice, 'voucherCodes'> | null
  codes: string[]
  onChange: (codes: string[]) => void
}) {
  const { t } = useTranslation('checkout')
  const id = useId()
  const [text, setText] = useState('')
  const tryVoucher = useTryVoucher()
  const code = text.trim().toUpperCase()
  const full = codes.length >= MAX_VOUCHER_CODES

  function apply() {
    if (!choice || !code) return
    if (codes.includes(code)) {
      setText('')
      return
    }

    tryVoucher.mutate(
      { choice: { ...choice, voucherCodes: codes }, code },
      {
        onSuccess: () => {
          onChange([...codes, code])
          setText('')
        },
      },
    )
  }

  return (
    <div className="grid gap-2">
      <Label htmlFor={id} className="flex items-center gap-1.5">
        <TicketPercentIcon className="size-4" /> {t('voucher.label')}
      </Label>
      <form
        className="flex gap-2"
        onSubmit={(event) => {
          event.preventDefault()
          apply()
        }}
      >
        <Input
          id={id}
          value={text}
          maxLength={32}
          disabled={full}
          placeholder={full ? t('voucher.full', { count: MAX_VOUCHER_CODES }) : t('voucher.placeholder')}
          className="h-10 rounded-xl font-mono uppercase"
          onChange={(event) => {
            setText(event.target.value)
            if (tryVoucher.isError) tryVoucher.reset()
          }}
        />
        <Button type="submit" variant="outline" className="h-10 rounded-xl" disabled={!choice || !code || full || tryVoucher.isPending}>
          {t('voucher.apply')}
        </Button>
      </form>
      <ServerError error={tryVoucher.error} fallback={t('voucher.failed')} />
      {codes.length > 0 && (
        <ul className="flex flex-wrap gap-1.5" aria-label={t('voucher.applied')}>
          {codes.map((c) => (
            <li key={c}>
              <Badge variant="secondary" className="gap-1 font-mono">
                {c}
                <button
                  type="button"
                  className="hover:text-destructive"
                  aria-label={t('voucher.remove', { code: c })}
                  onClick={() => onChange(codes.filter((x) => x !== c))}
                >
                  <XIcon className="size-3" />
                </button>
              </Badge>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
