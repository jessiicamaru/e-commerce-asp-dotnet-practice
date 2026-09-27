import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { Delivery } from '@/services/delivery'
import { TrackingLink } from '.'
import { trackingUrl } from './tracking-url'

const renderLink = (reference: string) =>
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <TrackingLink reference={reference} />
    </QueryClientProvider>,
  )

describe('TrackingLink (specs/098)', () => {
  it("links a reference to the carrier's page, named", async () => {
    vi.spyOn(Delivery, 'carrier').mockResolvedValue({ name: 'GHN', trackingUrlTemplate: 'https://ghn.example/track/{reference}' })
    renderLink('VN 123/A')

    const link = await screen.findByRole('link', { name: /VN 123\/A/ })
    expect(link).toHaveAttribute('href', 'https://ghn.example/track/VN%20123%2FA')
    expect(link).toHaveAttribute('target', '_blank')
    expect(link).toHaveTextContent('(GHN)')
  })

  it('shows the reference as text when the carrier has no tracking page', async () => {
    renderLink('VN-A')   // the setup's carrier has none

    expect(await screen.findByText('VN-A')).toBeInTheDocument()
    expect(screen.queryByRole('link')).toBeNull()
  })

  it('fills the reference in, escaped, and gives nothing without a template', () => {
    expect(trackingUrl('https://x.example/{reference}?a=1', 'A&B')).toBe('https://x.example/A%26B?a=1')
    expect(trackingUrl(null, 'A')).toBeNull()
  })
})
