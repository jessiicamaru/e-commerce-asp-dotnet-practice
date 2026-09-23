import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { RequireAuth } from '@/components/auth/require-auth'
import { RequireRole } from '@/components/auth/require-role'
import { MainLayout } from '@/layouts/main-layout'
import { SellerLayout } from '@/layouts/seller-layout'
import { AccountPage } from '@/pages/account'
import { AddressesPage } from '@/pages/addresses'
import { CartPage } from '@/pages/cart'
import { CatalogPage } from '@/pages/catalog'
import { CheckoutPage } from '@/pages/checkout'
import { OrderPage } from '@/pages/order'
import { OrdersPage } from '@/pages/orders'
import { ProductPage } from '@/pages/product'
import { ShopPage } from '@/pages/shop'
import { SellerProductPage } from '@/pages/shop-product'
import { ShopProductsPage } from '@/pages/shop-products'
import { NewProductPage } from '@/pages/shop-product-new'
import { ShopSalePage } from '@/pages/shop-sale'
import { ShopSalesPage } from '@/pages/shop-sales'
import { ShopPayoutsPage } from '@/pages/shop-payouts'
import { AdminLayout } from '@/layouts/admin-layout'
import { AdminOrderPage } from '@/pages/admin-order'
import { AdminPayoutsPage } from '@/pages/admin-payouts'
import { AdminAuditPage } from '@/pages/admin-audit'
import { AdminHome } from '@/pages/admin-home'
import { AdminUsersPage } from '@/pages/admin-users'
import { NotificationsPage } from '@/pages/notifications'
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
          <Route path="/sign-in" element={<SignInPage />} />
          <Route path="/sign-up" element={<SignUpPage />} />
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
            path="/cart"
            element={
              <RequireAuth>
                <CartPage />
              </RequireAuth>
            }
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
            path="/orders"
            element={
              <RequireAuth>
                <OrdersPage />
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
            <Route path="payouts" element={<ShopPayoutsPage />} />
          </Route>
          {/* The administrator's console (specs/038). Same bargain as /shop: RequireRole draws, the
              server decides. */}
          <Route
            path="/admin"
            element={
              <RequireRole role={['Admin', 'Moderator']}>
                <AdminLayout />
              </RequireRole>
            }
          >
            <Route index element={<AdminHome />} />
            <Route path="orders/:id" element={<AdminOrderPage />} />
            <Route path="payouts" element={<AdminPayoutsPage />} />
            <Route path="audit" element={<AdminAuditPage />} />
            <Route path="users" element={<AdminUsersPage />} />
          </Route>
          <Route path="*" element={<p>Not found.</p>} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}
