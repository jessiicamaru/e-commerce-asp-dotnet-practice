import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { BellIcon, CheckCheckIcon } from 'lucide-react'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useMarkRead, useNotifications, useUnreadCount } from '@/hooks/notifications'
import { describeNotification } from '@/utils/notifications'
import { cn } from '@/utils/shared'

/** How many the bell shows; the rest are on the notifications page. */
export const BELL_SIZE = 8

/**
 * The bell (specs/042): how many are unread - asked again every 30 seconds - and, when opened, the latest
 * few in the reader's words. Choosing one marks it read and goes where it points.
 */
export function NotificationBell() {
  const { t, i18n } = useTranslation('notifications')
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)
  const unread = useUnreadCount(true).data ?? 0
  const latest = useNotifications(1, BELL_SIZE, false, open)
  const mark = useMarkRead()

  return (
    <DropdownMenu open={open} onOpenChange={setOpen}>
      <DropdownMenuTrigger
        aria-label={unread > 0 ? t('bellUnread', { count: unread }) : t('bell')}
        className="hover:bg-secondary focus-visible:ring-ring/50 relative grid size-10 place-items-center rounded-full outline-none focus-visible:ring-3 [&_svg]:size-5"
      >
        <BellIcon />
        {unread > 0 && (
          <span className="bg-primary text-primary-foreground ring-card absolute -top-0.5 -right-0.5 grid h-4.5 min-w-4.5 place-items-center rounded-full px-1 text-[10px] leading-none font-bold ring-2">
            {unread > 99 ? '99+' : unread}
          </span>
        )}
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-80">
        <DropdownMenuGroup>
          <DropdownMenuLabel className="text-foreground py-2 text-sm font-semibold">{t('title')}</DropdownMenuLabel>
          {/* A menu item, not a button inside the label: in a menu, only items are reachable and announced. */}
          {unread > 0 && (
            <DropdownMenuItem className="text-primary text-xs font-medium" closeOnClick={false} onClick={() => mark.all.mutate()}>
              <CheckCheckIcon className="size-3.5" /> {t('markAll')}
            </DropdownMenuItem>
          )}
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        {latest.data?.items.length === 0 && <p className="text-muted-foreground px-3 py-6 text-center text-sm">{t('none')}</p>}
        <DropdownMenuGroup>
          {latest.data?.items.map((n) => (
            <DropdownMenuItem
              key={n.id}
              className="grid items-start gap-0.5 py-2"
              onClick={() => {
                if (!n.readAt) mark.one.mutate(n.id)
                if (n.link) navigate(n.link)
              }}
            >
              <span className={cn('text-sm whitespace-normal', !n.readAt && 'font-semibold')}>
                {!n.readAt && <span aria-hidden className="bg-primary mr-1.5 inline-block size-2 rounded-full" />}
                {describeNotification(t, n)}
              </span>
              <span className="text-muted-foreground text-xs">{new Date(n.createdAt).toLocaleString(i18n.language)}</span>
            </DropdownMenuItem>
          ))}
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        <DropdownMenuItem className="justify-center text-sm font-medium" onClick={() => navigate('/notifications')}>
          {t('seeAll')}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
