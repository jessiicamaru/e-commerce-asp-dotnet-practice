import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { EllipsisIcon, SearchIcon } from 'lucide-react'
import { PageTitle } from '@/components/seller/page-title'
import { Pager } from '@/components/shared/pager'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { ServerError } from '@/components/shared/server-error'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { InputGroup, InputGroupAddon, InputGroupInput } from '@/components/ui/input-group'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { PAGE_SIZE } from '@/constants/shared'
import { useAuth } from '@/context/auth/useAuth'
import { useAccountActions, useAccounts } from '@/hooks/accounts'
import { MODERATOR_MAX_LOCK_DAYS, type Account } from '@/services/accounts/types'
import { StopDialog, type Stop } from './stop-dialog'

/**
 * Staff look after people here (specs/043): find somebody by email or name, make them a moderator, or
 * stop an account.
 *
 * <p>
 * What each person is offered follows their role - an administrator grants, revokes and bans; a
 * moderator locks and unlocks - and nobody is offered an action on their own account or on an
 * administrator's. That is drawing. The server refuses each of those on its own, and its refusal is
 * shown in its own words.
 * </p>
 */
export function AdminUsersPage() {
  const { t, i18n } = useTranslation('admin')
  const { user, isAdmin } = useAuth()
  const [params, setParams] = useSearchParams()
  const search = params.get('search') ?? ''
  const page = Number(params.get('page') ?? '1') || 1
  const [draft, setDraft] = useState(search)
  const [stopping, setStopping] = useState<{ kind: Stop; account: Account } | null>(null)
  // When the page opened: what "days still to run" is measured from, fixed rather than read in render.
  const [now] = useState(() => Date.now())

  const accounts = useAccounts(search, page, PAGE_SIZE)
  const act = useAccountActions()
  const failed = [act.grant, act.revoke, act.lock, act.unlock, act.ban, act.liftBan].find((m) => m.isError)?.error

  const go = (next: { search?: string; page?: number }) => {
    const merged = new URLSearchParams()
    const q = next.search ?? search
    // Not "q": the header's catalogue search reads that one, and would echo the name typed here.
    if (q) merged.set('search', q)
    if (next.page && next.page > 1) merged.set('page', String(next.page))
    setParams(merged)
  }

  const statusOf = (a: Account) =>
    a.bannedAt ? (
      <Badge variant="destructive">{t('users.status.banned')}</Badge>
    ) : a.lockedUntil ? (
      <Badge variant="secondary">{t('users.status.locked', { until: new Date(a.lockedUntil).toLocaleString(i18n.language) })}</Badge>
    ) : (
      <Badge variant="outline">{t('users.status.active')}</Badge>
    )

  const done = (key: string, a: Account) => () => toast.success(t(key, { email: a.email }))

  return (
    <section className="grid gap-6">
      <PageTitle title={t('users.title')} subtitle={t('users.subtitle')} />

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
            aria-label={t('users.search')}
            placeholder={t('users.search')}
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
          />
        </InputGroup>
      </form>

      <ServerError error={failed} fallback={t('users.loadFailed')} />

      {accounts.isError ? (
        <ErrorMessage>{t('users.loadFailed')}</ErrorMessage>
      ) : accounts.isPending || !accounts.data ? (
        <LoadingRows />
      ) : accounts.data.totalCount === 0 ? (
        <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">{t('users.none')}</p>
      ) : (
        <>
          <div className="bg-card ring-border/60 overflow-x-auto rounded-3xl p-2 ring-1">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('users.columns.person')}</TableHead>
                  <TableHead>{t('users.columns.roles')}</TableHead>
                  <TableHead>{t('users.columns.status')}</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {accounts.data.items.map((a) => {
                  const self = a.id === user?.id
                  const admin = a.roles.includes('Admin')
                  const moderator = a.roles.includes('Moderator')
                  // Nobody stops themselves or an administrator; a moderator does not stop a moderator.
                  const stoppable = !self && !admin && (isAdmin || !moderator)
                  // Unlocking obeys the same limits (specs/050), and a moderator lifts only a lock they could
                  // have set - no more than MODERATOR_MAX_LOCK_DAYS still to run.
                  const withinReach =
                    !!a.lockedUntil && new Date(a.lockedUntil).getTime() - now <= MODERATOR_MAX_LOCK_DAYS * 86_400_000
                  const releasable = !self && (isAdmin || (!moderator && withinReach))

                  return (
                    <TableRow key={a.id}>
                      <TableCell className="whitespace-normal">
                        <p className="font-medium">
                          {a.firstName} {a.lastName}
                          {self && <span className="text-muted-foreground text-xs"> ({t('users.you')})</span>}
                        </p>
                        <p className="text-muted-foreground text-xs">{a.email}</p>
                      </TableCell>
                      <TableCell className="whitespace-normal">
                        <div className="flex flex-wrap gap-1">
                          {a.roles.map((r) => (
                            <Badge key={r} variant={r === 'Admin' || r === 'Moderator' ? 'default' : 'outline'}>
                              {t(`roles.${r}`, { defaultValue: r })}
                            </Badge>
                          ))}
                        </div>
                      </TableCell>
                      <TableCell>{statusOf(a)}</TableCell>
                      <TableCell className="text-right">
                        <DropdownMenu>
                          <DropdownMenuTrigger
                            render={<Button size="icon-sm" variant="ghost" className="rounded-full" />}
                            aria-label={t('users.actions', { email: a.email })}
                          >
                            <EllipsisIcon />
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end" className="min-w-52">
                            {isAdmin && !admin && !moderator && !a.bannedAt && (
                              <DropdownMenuItem onClick={() => act.grant.mutate(a.id, { onSuccess: done('users.granted', a) })}>
                                {t('users.grant')}
                              </DropdownMenuItem>
                            )}
                            {isAdmin && moderator && (
                              <DropdownMenuItem onClick={() => act.revoke.mutate(a.id, { onSuccess: done('users.revoked', a) })}>
                                {t('users.revoke')}
                              </DropdownMenuItem>
                            )}
                            {isAdmin && <DropdownMenuSeparator />}
                            {a.lockedUntil ? (
                              <DropdownMenuItem
                                disabled={!releasable}
                                onClick={() => act.unlock.mutate(a.id, { onSuccess: done('users.unlocked', a) })}
                              >
                                {t('users.unlock')}
                              </DropdownMenuItem>
                            ) : (
                              <DropdownMenuItem disabled={!stoppable} onClick={() => setStopping({ kind: 'lock', account: a })}>
                                {t('users.lock')}
                              </DropdownMenuItem>
                            )}
                            {isAdmin &&
                              (a.bannedAt ? (
                                <DropdownMenuItem onClick={() => act.liftBan.mutate(a.id, { onSuccess: done('users.banLifted', a) })}>
                                  {t('users.liftBan')}
                                </DropdownMenuItem>
                              ) : (
                                <DropdownMenuItem
                                  variant="destructive"
                                  disabled={!stoppable}
                                  onClick={() => setStopping({ kind: 'ban', account: a })}
                                >
                                  {t('users.ban')}
                                </DropdownMenuItem>
                              ))}
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    </TableRow>
                  )
                })}
              </TableBody>
            </Table>
          </div>
          <Pager page={page} pageSize={PAGE_SIZE} totalCount={accounts.data.totalCount} onChange={(next) => go({ page: next })} />
        </>
      )}

      <StopDialog
        stopping={stopping}
        maxDays={isAdmin ? undefined : MODERATOR_MAX_LOCK_DAYS}
        onClose={() => setStopping(null)}
        onConfirm={(kind, account, days, reason) => {
          if (kind === 'lock') act.lock.mutate({ id: account.id, days, reason }, { onSuccess: done('users.locked', account) })
          else act.ban.mutate({ id: account.id, reason }, { onSuccess: done('users.banned', account) })
          setStopping(null)
        }}
      />
    </section>
  )
}
