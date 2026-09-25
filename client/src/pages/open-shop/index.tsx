import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { PageTitle } from '@/components/seller/page-title'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { ServerError } from '@/components/shared/server-error'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { useAuth } from '@/context/auth/useAuth'
import { useApplyForShop, useMyShopApplications } from '@/hooks/shop-applications'
import type { ShopApplication } from '@/services/shop-applications/types'

/**
 * Asking to sell (specs/044): the form, and where every application so far stands.
 *
 * <p>
 * The form is offered only when nothing is waiting - the server refuses a second pending application
 * anyway, with a 409 shown in its words. An approval puts Seller on the account, but a session only
 * learns its roles when it is renewed, so "Go to my shop" renews it first rather than sending somebody to
 * a page that would bounce them.
 * </p>
 */
export function OpenShopPage() {
  const { t, i18n } = useTranslation('seller')
  const { user, isSeller, refreshSession } = useAuth()
  const navigate = useNavigate()
  const mine = useMyShopApplications()
  const apply = useApplyForShop()
  const [form, setForm] = useState({ shopName: '', description: '', phone: '' })
  const [opening, setOpening] = useState(false)

  if (mine.isError) return <ErrorMessage>{t('apply.failed')}</ErrorMessage>
  if (mine.isPending) return <LoadingRows />

  const applications = mine.data
  const waiting = applications.some((a) => a.status === 'Pending')
  const approved = applications.some((a) => a.status === 'Approved')

  const openShop = async () => {
    setOpening(true)
    if (isSeller || (await refreshSession())) navigate('/shop')
    setOpening(false)
  }

  const submit = (event: FormEvent) => {
    event.preventDefault()
    apply.mutate(form, {
      onSuccess: () => {
        toast.success(t('apply.sent'))
        setForm({ shopName: '', description: '', phone: '' })
      },
    })
  }

  return (
    <section className="mx-auto grid max-w-2xl gap-6">
      <PageTitle title={t('apply.title')} subtitle={t('apply.subtitle')} />

      {applications.length > 0 && (
        <div className="grid gap-3">
          <h2 className="font-semibold">{t('apply.history')}</h2>
          {applications.map((a) => (
            <Application key={a.id} application={a} language={i18n.language} />
          ))}
        </div>
      )}

      {(approved || isSeller) && (
        <Button className="justify-self-start rounded-full" disabled={opening} onClick={() => void openShop()}>
          {opening ? t('apply.opening') : t('apply.open')}
        </Button>
      )}

      {/* A shop is a public claim in the address's name (specs/063): the server refuses 403 anyway. */}
      {!waiting && !approved && !isSeller && user?.emailConfirmed === false && (
        <p role="alert" className="bg-card ring-border/60 rounded-3xl p-6 text-sm ring-1">
          {t('apply.confirmFirst', { email: user.email })}
        </p>
      )}

      {!waiting && !approved && !isSeller && user?.emailConfirmed !== false && (
        <form onSubmit={submit} className="bg-card ring-border/60 grid gap-4 rounded-3xl p-6 ring-1">
          {applications.some((a) => a.status === 'Rejected') && (
            <p className="text-muted-foreground text-sm">{t('apply.applyAgain')}</p>
          )}
          <div className="grid gap-1.5">
            <Label htmlFor="shopName">{t('apply.shopName')}</Label>
            <Input
              id="shopName"
              required
              maxLength={100}
              value={form.shopName}
              onChange={(event) => setForm({ ...form, shopName: event.target.value })}
            />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="description">{t('apply.description')}</Label>
            <Textarea
              id="description"
              maxLength={1000}
              value={form.description}
              onChange={(event) => setForm({ ...form, description: event.target.value })}
            />
            <p className="text-muted-foreground text-xs">{t('apply.descriptionHint')}</p>
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="phone">{t('apply.phone')}</Label>
            <Input
              id="phone"
              type="tel"
              maxLength={20}
              value={form.phone}
              onChange={(event) => setForm({ ...form, phone: event.target.value })}
            />
          </div>
          <ServerError error={apply.error} fallback={t('apply.failed')} />
          <Button type="submit" className="justify-self-start rounded-full" disabled={apply.isPending || !form.shopName.trim()}>
            {apply.isPending ? t('apply.submitting') : t('apply.submit')}
          </Button>
        </form>
      )}
    </section>
  )
}

function Application({ application: a, language }: { application: ShopApplication; language: string }) {
  const { t } = useTranslation('seller')
  const variant = a.status === 'Approved' ? 'default' : a.status === 'Rejected' ? 'destructive' : 'secondary'

  return (
    <article className="bg-card ring-border/60 grid gap-1 rounded-3xl p-4 ring-1">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="font-medium">{a.shopName}</p>
        <Badge variant={variant}>{t(`apply.status.${a.status}`)}</Badge>
      </div>
      <p className="text-muted-foreground text-xs">{new Date(a.createdAt).toLocaleString(language)}</p>
      <p className="text-sm">
        {a.status === 'Pending'
          ? t('apply.pendingBody')
          : a.status === 'Rejected'
            ? t('apply.rejectedBody', { reason: a.decisionReason })
            : t('apply.approvedBody')}
      </p>
    </article>
  )
}
