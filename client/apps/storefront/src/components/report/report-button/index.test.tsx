import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { Reports } from '@ecommerce/core/services/reports'
import { refusal } from '@ecommerce/core/test/refusal'
import { renderAsCustomer, renderSignedOut } from '@ecommerce/core/test/render'
import { ReportButton } from '.'

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('ReportButton (specs/101)', () => {
  it('is not offered to somebody who is not signed in', () => {
    renderSignedOut(<ReportButton targetType="Review" targetId="r1" />)
    expect(screen.queryByRole('button', { name: /Report/ })).toBeNull()
  })

  /** What is sent: the thing, the reason chosen, and the words trimmed - none rather than spaces. */
  it('sends the reason and the words, and nothing until a reason is chosen', async () => {
    const create = vi.spyOn(Reports, 'create').mockResolvedValue()
    const user = userEvent.setup()
    renderAsCustomer(<ReportButton targetType="Review" targetId="r1" />)

    await user.click(screen.getByRole('button', { name: 'Report this review' }))
    const dialog = await screen.findByRole('dialog')
    const send = within(dialog).getByRole('button', { name: 'Send report' })
    expect(send).toBeDisabled()

    await user.click(within(dialog).getByRole('radio', { name: 'Spam or advertising' }))
    await user.type(within(dialog).getByLabelText(/Anything staff should know/), '  Links to another shop  ')
    await user.click(send)

    await waitFor(() =>
      expect(create).toHaveBeenCalledWith({ targetType: 'Review', targetId: 'r1', reason: 'Spam', details: 'Links to another shop' }))
  })

  it('shows a refusal in the server’s words', async () => {
    vi.spyOn(Reports, 'create').mockRejectedValue(refusal(409, 'You have already reported this; staff will look at it.'))
    const user = userEvent.setup()
    renderAsCustomer(<ReportButton targetType="Product" targetId="p1" />)

    await user.click(screen.getByRole('button', { name: 'Report this product' }))
    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('radio', { name: 'Counterfeit or not as described' }))
    await user.click(within(dialog).getByRole('button', { name: 'Send report' }))

    expect(await within(dialog).findByText('You have already reported this; staff will look at it.')).toBeInTheDocument()
  })
})
