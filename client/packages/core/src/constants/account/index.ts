import { BellIcon, HeartIcon, KeyRoundIcon, MapPinIcon, PackageIcon, ShieldCheckIcon, StoreIcon, UserIcon } from 'lucide-react'
import { goToBackOffice } from '@ecommerce/core/utils/back-office'
import { backOfficeUrl } from '@ecommerce/core/config/apps'
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
    // The back office is another application (specs/137): its absolute address, opened by a full navigation.
    ...(isStaff ? [{ to: backOfficeUrl(), icon: ShieldCheckIcon, label: 'admin:goToBackOffice' }] : []),
  ]
}

/**
 * Goes to a destination (specs/137): a path is a page of this app, an absolute address is another application - the
 * back office - reached by a full navigation. A router asked to navigate to "http://..." would treat it as a path.
 */
export function followDestination(to: string, navigate: (to: string) => void) {
  if (to === backOfficeUrl()) void goToBackOffice()
  else if (/^https?:\/\//.test(to)) window.location.assign(to)
  else navigate(to)
}

/**
 * What a LINK to a destination does on a click (specs/140): the back office's is a handoff rather than the bare address,
 * so staff are asked only for their code. Undefined for every other destination - an ordinary link.
 */
export function destinationClick(to: string): ((event: { preventDefault(): void }) => void) | undefined {
  if (to !== backOfficeUrl()) return undefined
  return (event) => {
    event.preventDefault()
    void goToBackOffice()
  }
}
