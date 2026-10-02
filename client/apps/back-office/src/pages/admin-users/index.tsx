import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { EllipsisIcon, SearchIcon } from 'lucide-react'
import { CloseShop } from '@/components/close-shop'
import { PageTitle } from '@ecommerce/core/components/seller/page-title'
import { Pager } from '@ecommerce/core/components/shared/pager'
import { ErrorMessage, LoadingRows } from '@ecommerce/core/components/query-state'
import { ServerError } from '@ecommerce/core/components/shared/server-error'
import { TabStrip } from '@ecommerce/core/components/shared/tab-strip'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@ecommerce/ui/alert-dialog'
import { Badge } from '@ecommerce/ui/badge'
import { Button } from '@ecommerce/ui/button'
import { Checkbox } from '@ecommerce/ui/checkbox'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@ecommerce/ui/dropdown-menu'
import { InputGroup, InputGroupAddon, InputGroupInput } from '@ecommerce/ui/input-group'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@ecommerce/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@ecommerce/ui/table'
import { PAGE_SIZE } from '@ecommerce/core/constants/shared'
import { useAuth } from '@ecommerce/core/context/auth/useAuth'
import { useAccountActions, useAccounts } from '@ecommerce/core/hooks/accounts'
import { MODERATOR_MAX_LOCK_DAYS, type Account, ACCOUNT_ROLES, ACCOUNT_STATES, type AccountFilter } from '@ecommerce/core/services/accounts/types'
import { HistoryDialog } from './history-dialog'
import { StopDialog, type Stop } from './stop-dialog'

/**
 * Staff look after people here (specs/043): find somebody by email or name, make them a moderator, or
 * stop an account - and read what staff decided about them before (specs/100).
 *
 * <p>
 * What each person is offered follows their role - an administrator grants, revokes and bans; a
 * moderator locks and unlocks - and nobody is offered an action on their own account or on an
 * administrator's. That is drawing. The server refuses each of those on its own, and its refusal is
 * shown in its own words.
 * </p>
 * <p>
 * A deleted account (specs/112) is left out unless "Show deleted accounts" is ticked, and then reads Deleted with its
 * date and offers only its history - it was listed as Active with the menu to lock and ban (specs/123, #241).
 * </p>
 */
export function AdminUsersPage() {
  const { t, i18n } = useTranslation('admin')
  const { user, isAdmin } = useAuth()
  const [params, setParams] = useSearchParams()
  const search = params.get('search') ?? ''
  const page = Number(params.get('page') ?? '1') || 1
  const showDeleted = params.get('deleted') === '1'
  // By role and by state (specs/133, #249) - a 34-page list had only a name search.
  const role = (ACCOUNT_ROLES as readonly string[]).includes(params.get('role') ?? '') ? (params.get('role') as AccountFilter['role']) : ''
  const state = (ACCOUNT_STATES as readonly string[]).includes(params.get('state') ?? '') ? (params.get('state') as AccountFilter['state']) : ''
  const [draft, setDraft] = useState(search)
  const [stopping, setStopping] = useState<{ kind: Stop; account: Account } | null>(null)
  const [reading, setReading] = useState<Account | null>(null)
  // When the page opened: what "days still to run" is measured from, fixed rather than read in render.
  const [now] = useState(() => Date.now())

  const accounts = useAccounts(search, page, PAGE_SIZE, showDeleted, { role, state })
  const act = useAccountActions()
  const failed = [act.grant, act.revoke, act.lock, act.unlock, act.ban, act.liftBan, act.resetTwoFactor].find((m) => m.isError)?.error
  const [resetting, setResetting] = useState<Account | null>(null)
  // A seller's shop, closed from their row (specs/137): shops from before applications (specs/044) have no other place.
  const [closing, setClosing] = useState<Account | null>(null)

  const go = (next: { search?: string; page?: number; deleted?: boolean; role?: string; state?: string }) => {
    const merged = new URLSearchParams()
    const q = next.search ?? search
    // Not "q": the header's catalogue search reads that one, and would echo the name typed here.
    if (q) merged.set('search', q)
    if (next.deleted ?? showDeleted) merged.set('deleted', '1')
    const r = next.role ?? role
    const s = next.state ?? state
    if (r) merged.set('role', r)
    if (s) merged.set('state', s)
    if (next.page && next.page > 1) merged.set('page', String(next.page))
    setParams(merged)
  }

  // A banned seller's shop is closed too (specs/095) - said here, decided by the server.
  const statusOf = (a: Account) =>
    a.deletedAt ? (
      <Badge variant="outline" className="text-muted-foreground">
        {t('users.status.deleted', { date: new Date(a.deletedAt).toLocaleDateString(i18n.language) })}
      </Badge>
    ) : a.bannedAt ? (
      <Badge variant="destructive">
        {t(a.roles.includes('Seller') ? 'users.status.bannedShopClosed' : 'users.status.banned')}
      </Badge>
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

      <div className="flex flex-wrap items-center gap-3">
        <Select
          items={[{ value: '', label: t('users.filter.anyRole') }, ...ACCOUNT_ROLES.map((r) => ({ value: r, label: t(`roles.${r}`, { defaultValue: r }) }))]}
          value={role}
          onValueChange={(value) => go({ role: String(value ?? ''), page: 1 })}
        >
          <SelectTrigger className="bg-card h-9! w-44 rounded-xl" aria-label={t('users.filter.role')}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="">{t('users.filter.anyRole')}</SelectItem>
            {ACCOUNT_ROLES.map((r) => (
              <SelectItem key={r} value={r}>
                {t(`roles.${r}`, { defaultValue: r })}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <TabStrip
          tabs={(['', ...ACCOUNT_STATES] as const).map((value) => ({ value, label: t(`users.filter.state.${value || 'All'}`) }))}
          current={state ?? ''}
          onChange={(next) => go({ state: next, page: 1 })}
        />
        <label className="flex w-fit cursor-pointer items-center gap-2 text-sm">
          <Checkbox checked={showDeleted} onCheckedChange={(checked) => go({ deleted: !!checked, page: 1 })} />
          {t('users.showDeleted')}
        </label>
      </div>

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
                            {a.deletedAt ? t('users.deletedName') : `${a.firstName} ${a.lastName}`}
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
                              <DropdownMenuItem onClick={() => setReading(a)}>{t('users.showHistory')}</DropdownMenuItem>
                              {/* Nobody is left to act on: the server refuses with 409 AccountDeleted (specs/123). */}
                              {!a.deletedAt && (
                                <>
                              <DropdownMenuSeparator />
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
                              {a.roles.includes('Seller') && !a.deletedAt && (
                                <DropdownMenuItem onClick={() => setClosing(a)}>{t('users.closeShop')}</DropdownMenuItem>
                              )}
                              {isAdmin && a.twoFactorEnabled && a.id !== user?.id && (
                                <DropdownMenuItem onClick={() => setResetting(a)}>{t('users.resetTwoFactor')}</DropdownMenuItem>
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
                              </>
                            )}
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

      <HistoryDialog account={reading} onClose={() => setReading(null)} />

      {closing && (
        <CloseShop
          sellerId={closing.id}
          shopName={`${closing.firstName} ${closing.lastName}`.trim() || closing.email}
          open
          onOpenChange={(open) => !open && setClosing(null)}
        />
      )}
      <AlertDialog open={resetting !== null} onOpenChange={(open) => !open && setResetting(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('users.resetTwoFactorTitle', { email: resetting?.email ?? '' })}</AlertDialogTitle>
            <AlertDialogDescription>{t('users.resetTwoFactorBody')}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('action.cancel', { ns: 'common' })}</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              onClick={() => {
                const account = resetting
                if (account) {
                  act.resetTwoFactor.mutateAsync(account.id).then(() => toast.success(t('users.twoFactorReset', { email: account.email })), () => {})
                }
              }}
            >
              {t('users.resetTwoFactorConfirm')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

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
