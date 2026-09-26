import { Outlet } from 'react-router-dom'
import { ConfirmEmailBanner } from '@/components/layout/confirm-email-banner'
import { TopBar } from '@/components/layout/top-bar'
import { Toaster } from '@/components/ui/sonner'
import { useNotificationWording } from '@/hooks/notification-wording'

/**
 * The frame every page sits in: the bar, the page, and somewhere for toasts to appear.
 *
 * The page is wider than it was (6xl, not 5xl) because a product grid at four cards across needs the
 * room, and the bar floats over a tinted page rather than sitting on a white one.
 */
export function MainLayout() {
  // An administrator's rewording of the notices, over the bundled words (specs/078) - for every page, signed in or not.
  useNotificationWording()

  return (
    <div className="from-primary/8 min-h-dvh bg-linear-to-b via-transparent to-transparent">
      <TopBar />
      <main className="mx-auto max-w-6xl px-4 py-8">
        <ConfirmEmailBanner />
        <Outlet />
      </main>
      <Toaster />
    </div>
  )
}
