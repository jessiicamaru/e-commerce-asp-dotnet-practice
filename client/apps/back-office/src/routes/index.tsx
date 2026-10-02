import { lazy, Suspense } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { RequireRole } from '@ecommerce/core/components/auth/require-role'
import { LoadingRows } from '@ecommerce/core/components/query-state'
import { RequireStaff } from '@/components/require-staff'
import { AdminLayout } from '@/layouts/admin-layout'
import { BackOfficeLayout } from '@/layouts/back-office-layout'
import { AdminAuditPage } from '@/pages/admin-audit'
import { AdminCategoriesPage } from '@/pages/admin-categories'
import { AdminDeliveryPage } from '@/pages/admin-delivery'
import { AdminEmailDeliveryPage } from '@/pages/admin-email-delivery'
import { AdminHome } from '@/pages/admin-home'
import { AdminModerationPage } from '@/pages/admin-moderation'
import { AdminOrderPage } from '@/pages/admin-order'
import { AdminOrderSearchPage } from '@/pages/admin-order-search'
import { AdminOverviewPage } from '@/pages/admin-overview'
import { AdminPayoutsPage } from '@/pages/admin-payouts'
import { AdminProductsPage } from '@/pages/admin-products'
import { AdminQuestionsPage } from '@/pages/admin-questions'
import { AdminReportsPage } from '@/pages/admin-reports'
import { AdminReturnsPage } from '@/pages/admin-returns'
import { AdminReviewsPage } from '@/pages/admin-reviews'
import { AdminShopsPage } from '@/pages/admin-shops'
import { AdminUsersPage } from '@/pages/admin-users'
import { AdminVouchersPage } from '@/pages/admin-vouchers'
import { SignInPage } from '@/pages/sign-in'
import { AuthCallbackPage } from '@/pages/auth-callback'

// The two pages that bring a rich-text editor (specs/077): loaded when an administrator opens them.
const AdminEmailsPage = lazy(() => import('@/pages/admin-emails').then((page) => ({ default: page.AdminEmailsPage })))
const AdminWordingPage = lazy(() => import('@/pages/admin-wording').then((page) => ({ default: page.AdminWordingPage })))

/** An administrator's page within the console - a moderator is not offered it (specs/043). */
function AdminOnly({ children }: { children: React.ReactNode }) {
  return <RequireRole role={['Admin']}>{children}</RequireRole>
}

/**
 * Every address the back office answers (specs/136, 137): the console that lived at /admin in the storefront, at the
 * same paths without the prefix. Everything but signing in is behind the staff guard; RequireRole draws, the server
 * decides.
 */
export function AppRoutes() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/sign-in" element={<SignInPage />} />
        {/* Where the storefront's "Management platform" lands with a handoff (specs/140) - public, like signing in. */}
        <Route path="/auth/callback" element={<AuthCallbackPage />} />
        <Route
          element={
            <RequireStaff>
              <BackOfficeLayout />
            </RequireStaff>
          }
        >
          <Route element={<AdminLayout />}>
            <Route index element={<AdminHome />} />
            <Route path="orders/find" element={<AdminOnly><AdminOrderSearchPage /></AdminOnly>} />
            <Route path="categories" element={<AdminOnly><AdminCategoriesPage /></AdminOnly>} />
            <Route path="delivery" element={<AdminOnly><AdminDeliveryPage /></AdminOnly>} />
            <Route path="orders/:id" element={<AdminOrderPage />} />
            <Route path="returns" element={<AdminReturnsPage />} />
            <Route path="email-delivery" element={<AdminOnly><AdminEmailDeliveryPage /></AdminOnly>} />
            <Route path="vouchers" element={<AdminVouchersPage />} />
            <Route path="payouts" element={<AdminPayoutsPage />} />
            <Route path="audit" element={<AdminAuditPage />} />
            <Route path="users" element={<AdminUsersPage />} />
            <Route path="shops" element={<AdminShopsPage />} />
            <Route path="moderation" element={<AdminModerationPage />} />
            <Route path="products" element={<AdminProductsPage />} />
            <Route path="reviews" element={<AdminReviewsPage />} />
            <Route path="questions" element={<AdminQuestionsPage />} />
            <Route path="reports" element={<AdminReportsPage />} />
            <Route
              path="emails"
              element={<AdminOnly><Suspense fallback={<LoadingRows />}><AdminEmailsPage /></Suspense></AdminOnly>}
            />
            <Route
              path="notifications"
              element={<AdminOnly><Suspense fallback={<LoadingRows />}><AdminWordingPage /></Suspense></AdminOnly>}
            />
            <Route path="overview" element={<AdminOverviewPage />} />
          </Route>
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
