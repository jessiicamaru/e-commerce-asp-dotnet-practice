import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { LogOutIcon, MapPinIcon, PackageIcon, StoreIcon, UserIcon } from 'lucide-react'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import type { User } from '@/services/auth/types'
import { initialsOf } from './initials'

/**
 * Who is signed in, and everything that is about them, behind their initials.
 *
 * <p>
 * The account, the address book, the orders and - for a seller - the shop live here instead of as
 * five words across the bar. The shop entry is drawn only for a seller (specs/028), and that is
 * courtesy, not security: the endpoints behind it refuse anybody else on their own.
 * </p>
 */
export function UserMenu({ user, isSeller, onSignOut }: { user: User; isSeller: boolean; onSignOut: () => void }) {
  const { t } = useTranslation()
  const navigate = useNavigate()

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        className="focus-visible:ring-ring/50 rounded-full outline-none focus-visible:ring-3"
        aria-label={t('nav.account')}
      >
        <Avatar className="size-9">
          <AvatarFallback className="bg-primary text-primary-foreground text-xs font-bold">
            {initialsOf(user)}
          </AvatarFallback>
        </Avatar>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-60">
        <DropdownMenuGroup>
          <DropdownMenuLabel className="grid gap-0.5 py-2">
            <span className="text-foreground text-sm font-semibold">
              {user.firstName} {user.lastName}
            </span>
            <span className="truncate font-normal">{user.email}</span>
          </DropdownMenuLabel>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        <DropdownMenuGroup>
          <DropdownMenuItem onClick={() => navigate('/account')}>
            <UserIcon /> {t('nav.account')}
          </DropdownMenuItem>
          <DropdownMenuItem onClick={() => navigate('/orders')}>
            <PackageIcon /> {t('nav.orders')}
          </DropdownMenuItem>
          <DropdownMenuItem onClick={() => navigate('/addresses')}>
            <MapPinIcon /> {t('nav.addresses')}
          </DropdownMenuItem>
          {isSeller && (
            <DropdownMenuItem onClick={() => navigate('/shop')}>
              <StoreIcon /> {t('seller:nav')}
            </DropdownMenuItem>
          )}
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        <DropdownMenuItem variant="destructive" onClick={onSignOut}>
          <LogOutIcon /> {t('nav.signOut')}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
