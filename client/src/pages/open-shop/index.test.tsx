import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { ShopApplications } from '@/services/shop-applications'
import type { ShopApplication } from '@/services/shop-applications/types'
import { refusal } from '@/test/refusal'
import { renderAsCustomer } from '@/test/render'
import { OpenShopPage } from '.'

const application = (over: Partial<ShopApplication> = {}): ShopApplication => ({
  id: 'a1', userId: 'u1', applicantEmail: null, applicantName: null, shopName: 'Lan Film', description: null, phone: null,
  status: 'Pending', decisionReason: null, createdAt: '2026-09-24T08:00:00Z', decidedAt: null, ...over,
})

function renderPage() {
  return renderAsCustomer(
    <Routes>
      <Route path="/open-shop" element={<OpenShopPage />} />
      <Route path="/shop" element={<p>the shop</p>} />
    </Routes>,
    '/open-shop',
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('OpenShopPage (specs/044)', () => {
  it('sends the application with what was typed', async () => {
    vi.spyOn(ShopApplications, 'mine').mockResolvedValue([])
    const apply = vi.spyOn(ShopApplications, 'apply').mockResolvedValue(application())
    const user = userEvent.setup()
    renderPage()

    await user.type(await screen.findByLabelText('Shop name'), 'Lan Film')
    await user.type(screen.getByLabelText('What will you sell?'), 'Film cameras')
    await user.click(screen.getByRole('button', { name: 'Send the application' }))

    await waitFor(() => expect(apply).toHaveBeenCalledWith({ shopName: 'Lan Film', description: 'Film cameras', phone: '' }))
  })

  /** One waits at a time - the form is not offered while an application is pending. */
  it('shows a pending application and offers no second one', async () => {
    vi.spyOn(ShopApplications, 'mine').mockResolvedValue([application()])
    renderPage()

    expect(await screen.findByText('Waiting for review')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Send the application' })).not.toBeInTheDocument()
  })

  it('says why it was rejected and lets them apply again', async () => {
    vi.spyOn(ShopApplications, 'mine').mockResolvedValue([application({ status: 'Rejected', decisionReason: 'Tell us what you sell' })])
    renderPage()

    expect(await screen.findByText('Reason: Tell us what you sell')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Send the application' })).toBeInTheDocument()
  })

  /** Approved: the session is renewed first, so the shop page sees the Seller role and does not bounce. */
  it('renews the session and goes to the shop once approved', async () => {
    vi.spyOn(ShopApplications, 'mine').mockResolvedValue([application({ status: 'Approved' })])
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Go to my shop' }))

    expect(await screen.findByText('the shop')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Send the application' })).not.toBeInTheDocument()
  })

  it('shows the server refusal in its words', async () => {
    vi.spyOn(ShopApplications, 'mine').mockResolvedValue([])
    vi.spyOn(ShopApplications, 'apply').mockRejectedValue(refusal(409, 'An application is already waiting for review.'))
    const user = userEvent.setup()
    renderPage()

    await user.type(await screen.findByLabelText('Shop name'), 'Lan Film')
    await user.click(screen.getByRole('button', { name: 'Send the application' }))

    expect(await screen.findByText('An application is already waiting for review.')).toBeInTheDocument()
  })
})
