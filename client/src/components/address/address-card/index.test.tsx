import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { AddressCard } from '.'

const home = {
  id: 'a1', isDefault: false, recipientName: 'Lan Pham', line1: '12 Ly Thuong Kiet', line2: null,
  city: 'Ha Noi', region: null, postalCode: '100000', country: 'VN', phone: null,
}

function renderCard(onDelete = vi.fn()) {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <MemoryRouter>
        <AddressCard address={home} onMakeDefault={vi.fn()} onDelete={onDelete} />
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return onDelete
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('AddressCard', () => {
  it('asks before deleting, and deletes only when confirmed', async () => {
    const user = userEvent.setup()
    const onDelete = renderCard()

    await user.click(screen.getByRole('button', { name: /Delete/ }))
    expect(await screen.findByText('Delete this address?')).toBeInTheDocument()
    expect(onDelete).not.toHaveBeenCalled()

    await user.click(screen.getAllByRole('button', { name: /Delete/ }).at(-1)!)
    expect(onDelete).toHaveBeenCalledOnce()
  })

  /**
   * ⚠️ The confirming button closes the dialog. base-nova's `AlertDialogAction` was a plain button and
   * did not, which left the dialog over the page - hiding whatever the page then had to say, like a
   * refusal (specs/038). It is fixed once, in `ui/alert-dialog.tsx`; this is what fails if a
   * `shadcn add --overwrite` undoes it.
   */
  it('closes the dialog once confirmed', async () => {
    const user = userEvent.setup()
    renderCard()

    await user.click(screen.getByRole('button', { name: /Delete/ }))
    await screen.findByRole('alertdialog')
    await user.click(screen.getAllByRole('button', { name: /Delete/ }).at(-1)!)

    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument())
  })
})
