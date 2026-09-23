import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { RequireAuth } from '@/components/auth/require-auth'
import { RequireRole } from '@/components/auth/require-role'
import { MainLayout } from '@/layouts/main-layout'
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
import { NewProductPage } from '@/pages/shop-product-new'
import { ShopSalePage } from '@/pages/shop-sale'
import { ShopSalesPage } from '@/pages/shop-sales'
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
          {/* The seller's own pages (specs/028). RequireRole decides what to DRAW; the server
              decides what to allow, and answers the token rather than the route. */}
          <Route
            path="/shop"
            element={
              <RequireRole role="Seller">
                <ShopPage />
              </RequireRole>
            }
          />
          <Route
            path="/shop/products/new"
            element={
              <RequireRole role="Seller">
                <NewProductPage />
              </RequireRole>
            }
          />
          <Route
            path="/shop/products/:id"
            element={
              <RequireRole role="Seller">
                <SellerProductPage />
              </RequireRole>
            }
          />
          <Route
            path="/shop/sales"
            element={
              <RequireRole role="Seller">
                <ShopSalesPage />
              </RequireRole>
            }
          />
          <Route
            path="/shop/sales/:id"
            element={
              <RequireRole role="Seller">
                <ShopSalePage />
              </RequireRole>
            }
          />
          <Route path="*" element={<p>Not found.</p>} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}
