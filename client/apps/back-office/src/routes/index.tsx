import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { RequireStaff } from '@/components/require-staff'
import { BackOfficeLayout } from '@/layouts/back-office-layout'
import { HomePage } from '@/pages/home'
import { SignInPage } from '@/pages/sign-in'

/** Every address the back office answers. Everything but signing in is behind the staff guard. */
export function AppRoutes() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/sign-in" element={<SignInPage />} />
        <Route
          element={
            <RequireStaff>
              <BackOfficeLayout />
            </RequireStaff>
          }
        >
          <Route index element={<HomePage />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
