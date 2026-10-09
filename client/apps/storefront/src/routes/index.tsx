import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { RequireAuth } from '@/components/auth/require-auth'
import { RequireRole } from '@ecommerce/core/components/auth/require-role'
import { MainLayout } from '@/layouts/main-layout'
import { SellerLayout } from '@/layouts/seller-layout'
import { AccountPage } from '@/pages/account'
import { AccountTwoFactorPage } from '@/pages/account-two-factor'
import { AddressesPage } from '@/pages/addresses'
import { CartPage } from '@/pages/cart'
import { CatalogPage } from '@/pages/catalog'
import { CheckoutPage } from '@/pages/checkout'
import { OrderPage } from '@/pages/order'
import { PaymentReturnPage } from '@/pages/payment-return'
import { OrdersPage } from '@/pages/orders'
import { SavedPage } from '@/pages/saved'
import { ShopInsightsPage } from '@/pages/shop-insights'
import { ProductPage } from '@/pages/product'
import { ShopFrontPage } from '@/pages/shop-front'
import { ShopPage } from '@/pages/shop'
import { SellerProductPage } from '@/pages/shop-product'
import { ShopProductsPage } from '@/pages/shop-products'
import { NewProductPage } from '@/pages/shop-product-new'
import { ShopSalePage } from '@/pages/shop-sale'
import { ShopSalesPage } from '@/pages/shop-sales'
import { ShopPayoutsPage } from '@/pages/shop-payouts'
import { ShopReturnsPage } from '@/pages/shop-returns'
import { ShopVouchersPage } from '@/pages/shop-vouchers'
import { ShopQuestionsPage } from '@/pages/shop-questions'
import { OpenShopPage } from '@/pages/open-shop'
import { NotificationsPage } from '@/pages/notifications'
import { ConfirmEmailPage } from '@/pages/confirm-email'
import { ForgotPasswordPage } from '@/pages/forgot-password'
import { ResetPasswordPage } from '@/pages/reset-password'
import { NotFoundPage } from '@/pages/not-found'
import { ToBackOffice } from '@/components/layout/to-back-office'
import { SignInPage } from '@/pages/sign-in'
import { SignUpPage } from '@/pages/sign-up'
import { StatusPage } from '@/pages/status'

/** Every address the storefront answers. Anything behind RequireAuth needs a signed-in customer. */

export function AppRoutes() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<MainLayout />}>
          <Route path="/" element={<CatalogPage />} />
          <Route path="/products/:id" element={<ProductPage />} />
          <Route path="/shops/:sellerId" element={<ShopFrontPage />} />
          <Route path="/sign-in" element={<SignInPage />} />
          <Route path="/sign-up" element={<SignUpPage />} />
          <Route path="/forgot-password" element={<ForgotPasswordPage />} />
          <Route path="/reset-password" element={<ResetPasswordPage />} />
          <Route path="/confirm-email" element={<ConfirmEmailPage />} />
          <Route path="/status" element={<StatusPage />} />
          <Route
            path="/account"
            element={
              <RequireAuth>
                <AccountPage />
              </RequireAuth>
            }
          />
          <Route
            path="/account/two-factor"
            element={
              <RequireAuth>
                <AccountTwoFactorPage />
              </RequireAuth>
            }
          />
          <Route
            path="/cart"
            // Signed out it shows this browser's cart (specs/162); checkout still needs an account.
            element={<CartPage />}
          />
          <Route
            path="/addresses"
            element={
              <RequireAuth>
                <AddressesPage />
              </RequireAuth>
            }
          />
          <Route
            path="/checkout"
            element={
              <RequireAuth>
                <CheckoutPage />
              </RequireAuth>
            }
          />
          <Route
            path="/notifications"
            element={
              <RequireAuth>
                <NotificationsPage />
              </RequireAuth>
            }
          />
          <Route
            path="/open-shop"
            element={
              <RequireAuth>
                <OpenShopPage />
              </RequireAuth>
            }
          />
          <Route
            path="/orders"
            element={
              <RequireAuth>
                <OrdersPage />
              </RequireAuth>
            }
          />
          <Route
            path="/saved"
            element={
              <RequireAuth>
                <SavedPage />
              </RequireAuth>
            }
          />
          {/* Where VNPay sends the customer back (specs/143): it only names the order to show. */}
          <Route
            path="/payment/vnpay-return"
            element={
              <RequireAuth>
                <PaymentReturnPage />
              </RequireAuth>
            }
          />
          <Route
            path="/orders/:id"
            element={
              <RequireAuth>
                <OrderPage />
              </RequireAuth>
            }
          />
          {/* The seller's own pages (specs/028), all inside one frame with the same way around.
              RequireRole decides what to DRAW; the server decides what to allow, and answers the
              token rather than the route. */}
          <Route
            path="/shop"
            element={
              <RequireRole role="Seller">
                <SellerLayout />
              </RequireRole>
            }
          >
            <Route index element={<ShopPage />} />
            <Route path="products" element={<ShopProductsPage />} />
            <Route path="products/new" element={<NewProductPage />} />
            <Route path="products/:id" element={<SellerProductPage />} />
            <Route path="sales" element={<ShopSalesPage />} />
            <Route path="sales/:id" element={<ShopSalePage />} />
            <Route path="returns" element={<ShopReturnsPage />} />
            <Route path="payouts" element={<ShopPayoutsPage />} />
            <Route path="insights" element={<ShopInsightsPage />} />
            <Route path="vouchers" element={<ShopVouchersPage />} />
            <Route path="questions" element={<ShopQuestionsPage />} />
          </Route>
          {/* The console moved to the back office (specs/137): an old address - a bookmark, an email, a notice - is the
              same page there. */}
          <Route path="/admin/*" element={<ToBackOffice />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}
