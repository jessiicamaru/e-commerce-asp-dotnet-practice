import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { FlagIcon } from 'lucide-react'
import { ServerError } from '@/components/shared/server-error'
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
import { RadioGroup, RadioGroupItem } from '@ecommerce/ui/radio-group'
import { Textarea } from '@ecommerce/ui/textarea'
import { useAuth } from '@ecommerce/core/context/auth/useAuth'
import { useReport } from '@ecommerce/core/hooks/reports'
import { REPORT_REASONS, type ReportReason, type ReportTarget } from '@ecommerce/core/services/reports/types'

/**
 * "Report" on a review, a question or a product (specs/101): a reason from a short list and, optionally, a few words.
 * Signed-in shoppers only - nobody else is offered it. The server says no to a second open report, to your own words
 * and to anything off the shelf; its refusal is shown as it words it.
 */
export function ReportButton({ targetType, targetId }: { targetType: ReportTarget; targetId: string }) {
  const { t } = useTranslation('catalog')
  const { user } = useAuth()
  const report = useReport()
  const [open, setOpen] = useState(false)
  const [reason, setReason] = useState<ReportReason | ''>('')
  const [details, setDetails] = useState('')

  if (!user) return null

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) {
          setReason('')
          setDetails('')
          report.reset()
        }
      }}
    >
      <DialogTrigger
        render={<Button variant="ghost" size="sm" className="text-muted-foreground h-8 rounded-full px-2 text-xs" />}
        aria-label={t(`report.open.${targetType}`)}
      >
        <FlagIcon className="size-3.5" /> {t('report.action')}
      </DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <form
          className="grid gap-4"
          onSubmit={(event) => {
            event.preventDefault()
            if (!reason) return
            report
              .mutateAsync({ targetType, targetId, reason, details: details.trim() || null })
              .then(() => {
                toast.success(t('report.sent'))
                setOpen(false)
              }, () => {})
          }}
        >
          <DialogHeader>
            <DialogTitle>{t(`report.title.${targetType}`)}</DialogTitle>
            <DialogDescription>{t('report.body')}</DialogDescription>
          </DialogHeader>
          <RadioGroup value={reason} onValueChange={(value) => setReason(value as ReportReason)} className="gap-2">
            {REPORT_REASONS.map((r) => (
              <Label key={r} htmlFor={`report-${targetId}-${r}`} className="flex cursor-pointer items-center gap-3 font-normal">
                <RadioGroupItem value={r} id={`report-${targetId}-${r}`} />
                {t(`report.reason.${r}`)}
              </Label>
            ))}
          </RadioGroup>
          <div className="grid gap-2">
            <Label htmlFor={`report-${targetId}-details`}>{t('report.details')}</Label>
            <Textarea
              id={`report-${targetId}-details`}
              value={details}
              maxLength={500}
              rows={3}
              onChange={(event) => setDetails(event.target.value)}
            />
          </div>
          <ServerError error={report.error} fallback={t('report.failed')} />
          <DialogFooter>
            <DialogClose render={<Button type="button" variant="ghost" />}>{t('action.cancel', { ns: 'common' })}</DialogClose>
            <Button type="submit" disabled={!reason || report.isPending}>
              {t('report.send')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
