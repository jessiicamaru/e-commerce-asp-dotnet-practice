import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { Delivery } from '@ecommerce/core/services/delivery'
import type { DeliverySettings } from '@ecommerce/core/services/delivery/types'
import { refusal } from '@ecommerce/core/test/refusal'
import { renderAsAdmin } from '@ecommerce/core/test/render'
import { AdminDeliveryPage } from '.'

const settings = (): DeliverySettings => ({
  options: [
    { code: 'standard', name: 'Standard delivery', isActive: true, sortOrder: 0, prices: { VND: 30000, USD: 2 }, minDays: 3, maxDays: 5 },
    { code: 'express', name: 'Express delivery', isActive: true, sortOrder: 1, prices: { VND: 60000 }, minDays: null, maxDays: null },
  ],
  carrier: { name: 'Shop delivery', trackingUrlTemplate: null },
})

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Delivery, 'settings').mockResolvedValue(settings())
})

describe('AdminDeliveryPage (specs/098)', () => {
  it('reprices an option; a currency left empty is not offered in it', async () => {
    const save = vi.spyOn(Delivery, 'saveOption').mockResolvedValue(settings().options[1])
    const user = userEvent.setup()
    renderAsAdmin(<AdminDeliveryPage />, '/admin/delivery')

    const express = await screen.findByRole('form', { name: 'Express delivery' })
    const vnd = within(express).getByLabelText('Price in VND')
    await user.clear(vnd)
    await user.type(vnd, '75000')
    await user.click(within(express).getByRole('button', { name: 'Save' }))

    await waitFor(() =>
      expect(save).toHaveBeenCalledWith({
        code: 'express', name: 'Express delivery', isActive: true, sortOrder: 1, prices: { VND: 75000 }, minDays: null, maxDays: null,
      }),
    )
    expect(within(express).getByLabelText('Code')).toHaveAttribute('readonly')
  })

  it('adds an option under a new code', async () => {
    const save = vi.spyOn(Delivery, 'saveOption').mockResolvedValue(settings().options[0])
    const user = userEvent.setup()
    renderAsAdmin(<AdminDeliveryPage />, '/admin/delivery')

    const added = await screen.findByRole('form', { name: 'New option' })
    await user.type(within(added).getByLabelText('Code'), 'same-day')
    await user.type(within(added).getByLabelText('Name'), 'Same day')
    await user.type(within(added).getByLabelText('Price in VND'), '90000')
    await user.click(within(added).getByRole('button', { name: 'Add' }))

    await waitFor(() => expect(save).toHaveBeenCalledWith(expect.objectContaining({ code: 'same-day', name: 'Same day', prices: { VND: 90000 } })))
  })

  it('shows why the last option on offer cannot be turned off', async () => {
    vi.spyOn(Delivery, 'saveOption').mockRejectedValue(refusal(409, 'This is the last delivery option on offer; turn another on first.'))
    const user = userEvent.setup()
    renderAsAdmin(<AdminDeliveryPage />, '/admin/delivery')

    const standard = await screen.findByRole('form', { name: 'Standard delivery' })
    await user.click(within(standard).getByLabelText('Offered'))
    await user.click(within(standard).getByRole('button', { name: 'Save' }))

    expect(await within(standard).findByText('This is the last delivery option on offer; turn another on first.')).toBeInTheDocument()
  })

  it("saves the carrier's name and tracking address, and an empty address as none", async () => {
    const save = vi.spyOn(Delivery, 'saveCarrier').mockResolvedValue({ name: 'GHN', trackingUrlTemplate: null })
    const user = userEvent.setup()
    renderAsAdmin(<AdminDeliveryPage />, '/admin/delivery')

    const name = await screen.findByLabelText('Carrier')
    await user.clear(name)
    await user.type(name, 'GHN')
    await user.click(screen.getAllByRole('button', { name: 'Save' })[0])
    await waitFor(() => expect(save).toHaveBeenLastCalledWith({ name: 'GHN', trackingUrlTemplate: null }))

    await user.type(screen.getByLabelText('Tracking address'), 'https://ghn.example/track/{{reference}')
    await user.click(screen.getAllByRole('button', { name: 'Save' })[0])
    await waitFor(() => expect(save).toHaveBeenLastCalledWith({ name: 'GHN', trackingUrlTemplate: 'https://ghn.example/track/{reference}' }))
  })
})

describe('AdminDeliveryPage: how long an option takes (specs/134, #253)', () => {
  it('shows the time stored and sends the one typed, an empty one as none', async () => {
    const save = vi.spyOn(Delivery, 'saveOption').mockResolvedValue(settings().options[0])
    const user = userEvent.setup()
    renderAsAdmin(<AdminDeliveryPage />, '/admin/delivery')

    const standard = await screen.findByRole('form', { name: 'Standard delivery' })
    expect(within(standard).getByLabelText('Soonest (days)')).toHaveValue(3)
    expect(within(standard).getByLabelText('Latest (days)')).toHaveValue(5)
    await user.clear(within(standard).getByLabelText('Latest (days)'))
    await user.type(within(standard).getByLabelText('Latest (days)'), '7')
    await user.click(within(standard).getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(save).toHaveBeenLastCalledWith(expect.objectContaining({ code: 'standard', minDays: 3, maxDays: 7 })))

    await user.clear(within(standard).getByLabelText('Soonest (days)'))
    await user.clear(within(standard).getByLabelText('Latest (days)'))
    await user.click(within(standard).getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(save).toHaveBeenLastCalledWith(expect.objectContaining({ code: 'standard', minDays: null, maxDays: null })))
  })

  it("shows the server's words for a time that is not one", async () => {
    vi.spyOn(Delivery, 'saveOption').mockRejectedValue(refusal(400, 'The soonest day cannot be after the latest.'))
    const user = userEvent.setup()
    renderAsAdmin(<AdminDeliveryPage />, '/admin/delivery')

    const standard = await screen.findByRole('form', { name: 'Standard delivery' })
    await user.type(within(standard).getByLabelText('Soonest (days)'), '9')
    await user.click(within(standard).getByRole('button', { name: 'Save' }))

    expect(await within(standard).findByText('The soonest day cannot be after the latest.')).toBeInTheDocument()
  })
})
