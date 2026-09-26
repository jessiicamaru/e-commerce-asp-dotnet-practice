import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { RotateCcwIcon, SearchIcon } from 'lucide-react'
import { PageTitle } from '@/components/seller/page-title'
import { Pager } from '@/components/shared/pager'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { ServerError } from '@/components/shared/server-error'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { InputGroup, InputGroupAddon, InputGroupInput } from '@/components/ui/input-group'
import { PAGE_SIZE } from '@/constants/shared'
import { useOutgoingEmails, useRetryEmail } from '@/hooks/outgoing-email'
import { OUTGOING_EMAIL_STATES, type OutgoingEmailStatus } from '@/services/outgoing-email/types'
import { cn } from '@/utils/shared'

/**
 * What became of the emails the shop sent (specs/087, #175). It opens on the failed ones - an email nobody received,
 * with the reason - because those are the ones a person has to do something about. A failed email can be sent again;
 * a reset or confirmation link cannot, because it has expired - the person asks for a new one.
 *
 * <p>No email's data is shown: the server does not send it, and for a reset link it is a token.</p>
 */
export function AdminEmailDeliveryPage() {
  const { t, i18n } = useTranslation('admin')
  const [params, setParams] = useSearchParams()
  const asked = params.get('status')
  const status: OutgoingEmailStatus = OUTGOING_EMAIL_STATES.includes(asked as OutgoingEmailStatus) ? (asked as OutgoingEmailStatus) : 'Failed'
  const search = params.get('search') ?? ''
  const page = Number(params.get('page') ?? '1') || 1
  const [draft, setDraft] = useState(search)
  const emails = useOutgoingEmails(status, search, page, PAGE_SIZE)
  const retry = useRetryEmail()

  const go = (next: { status?: OutgoingEmailStatus; search?: string; page?: number }) => {
    const values = { status, search, page, ...next }
    setParams({ status: values.status, ...(values.search ? { search: values.search } : {}), page: String(values.page) })
  }
  const at = (value: string | null) => (value ? new Date(value).toLocaleString(i18n.language) : '')

  return (
    <section className="grid gap-6">
      <PageTitle title={t('delivery.title')} subtitle={t('delivery.subtitle')} />

      <div role="tablist" className="bg-card ring-border/60 flex flex-wrap gap-1 justify-self-start rounded-3xl p-1 ring-1">
        {OUTGOING_EMAIL_STATES.map((state) => (
          <button
            key={state}
            type="button"
            role="tab"
            aria-selected={state === status}
            onClick={() => go({ status: state, page: 1 })}
            className={cn(
              'rounded-full px-4 py-1.5 text-sm font-medium transition-colors',
              state === status ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {t(`delivery.state.${state}`)}
          </button>
        ))}
      </div>

      <form
        onSubmit={(event) => {
          event.preventDefault()
          go({ search: draft.trim(), page: 1 })
        }}
      >
        <InputGroup className="h-10 rounded-full">
          <InputGroupAddon>
            <SearchIcon />
          </InputGroupAddon>
          <InputGroupInput
            aria-label={t('delivery.search')}
            placeholder={t('delivery.search')}
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
          />
        </InputGroup>
      </form>

      <ServerError error={retry.error} fallback={t('delivery.retryFailed')} />

      {emails.isError ? (
        <ErrorMessage>{t('delivery.loadFailed')}</ErrorMessage>
      ) : emails.isPending || !emails.data ? (
        <LoadingRows />
      ) : emails.data.totalCount === 0 ? (
        <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">{t('delivery.none')}</p>
      ) : (
        <>
          <ul className="grid gap-3">
            {emails.data.items.map((email) => (
              <li key={email.id} className="bg-card ring-border/60 grid gap-1 rounded-3xl p-4 ring-1">
                <span className="flex flex-wrap items-center gap-2 font-medium">
                  {t(`emails.template.${email.template}`, { defaultValue: email.template })}
                  <Badge variant="outline">{t(`emails.language.${email.language}`, { defaultValue: email.language })}</Badge>
                  <span className="text-muted-foreground text-sm font-normal">
                    {email.recipientEmail ?? t('delivery.noRecipient')}
                  </span>
                </span>
                <span className="text-muted-foreground text-xs">
                  {email.status === 'Sent'
                    ? t('delivery.sentAt', { at: at(email.sentAt) })
                    : t('delivery.attempts', { count: email.attempts, at: at(email.createdAt) })}
                </span>
                {email.lastError && <span className="text-destructive text-sm">{email.lastError}</span>}
                {email.status === 'Failed' && (
                  email.canRetry ? (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      className="justify-self-start"
                      disabled={retry.isPending}
                      onClick={() =>
                        retry.mutateAsync(email.id).then(() => toast.success(t('delivery.retried')), () => {})
                      }
                    >
                      <RotateCcwIcon />
                      {t('delivery.retry')}
                    </Button>
                  ) : (
                    <span className="text-muted-foreground text-xs">{t('delivery.linkExpired')}</span>
                  )
                )}
              </li>
            ))}
          </ul>
          <Pager page={page} pageSize={PAGE_SIZE} totalCount={emails.data.totalCount} onChange={(next) => go({ page: next })} />
        </>
      )}
    </section>
  )
}
