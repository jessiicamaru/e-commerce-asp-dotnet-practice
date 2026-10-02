import { BellIcon, HeartIcon, KeyRoundIcon, MapPinIcon, PackageIcon, ShieldCheckIcon, StoreIcon, UserIcon } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'

/** One place a signed-in person can go from their account: where, with which icon, and the words for it. */
export interface AccountDestination {
  to: string
  icon: LucideIcon
  /** An i18n key with its namespace: `common:nav.orders`. */
  label: string
}

/**
 * Everywhere a signed-in person's account leads, in order (specs/127, #254) - the ONE list the avatar menu, the phone
 * menu and the account page draw. Each kept its own before, written by hand, and each was missing something.
 *
 * <p>
 * Drawing only: a seller is offered their shop and anybody else the way to open one, staff the console - the pages
 * behind them refuse whom they refuse on their own. The cart is not here: it is in the header on every width, with
 * its count.
 * </p>
 */
export function accountDestinations({ isSeller, isStaff }: { isSeller: boolean; isStaff: boolean }): AccountDestination[] {
  return [
    { to: '/account', icon: UserIcon, label: 'common:nav.account' },
    { to: '/orders', icon: PackageIcon, label: 'common:nav.orders' },
    { to: '/saved', icon: HeartIcon, label: 'common:nav.saved' },
    { to: '/notifications', icon: BellIcon, label: 'common:nav.notifications' },
    { to: '/addresses', icon: MapPinIcon, label: 'common:nav.addresses' },
    { to: '/account/two-factor', icon: KeyRoundIcon, label: 'common:nav.twoFactor' },
    // Anybody signed in may ask to sell (specs/044); the page says where their application stands.
    isSeller
      ? { to: '/shop', icon: StoreIcon, label: 'seller:nav' }
      : { to: '/open-shop', icon: StoreIcon, label: 'seller:openShop' },
    ...(isStaff ? [{ to: '/admin', icon: ShieldCheckIcon, label: 'admin:nav' }] : []),
  ]
}
