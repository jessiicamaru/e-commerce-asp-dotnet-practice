import { Outlet } from 'react-router-dom'
import { TopBar } from '@/components/layout/top-bar'
import { Toaster } from '@/components/ui/sonner'

/** The frame every page sits in: the top bar, the page, and somewhere for toasts to appear. */
export function MainLayout() {
  return (
    <>
      <TopBar />
      <main className="mx-auto max-w-5xl px-4 py-6">
        <Outlet />
      </main>
      <Toaster />
    </>
  )
}
