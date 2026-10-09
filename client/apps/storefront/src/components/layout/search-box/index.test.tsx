import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { Product } from '@ecommerce/core/services/product'
import { renderSignedOut } from '@ecommerce/core/test/render'
import { SearchBox } from '.'

const answer = {
  products: [
    { id: 'p1', name: 'Sony Alpha A7 IV', imageUrl: null, price: 52_000_000, currency: 'VND', priceVaries: false },
    { id: 'p2', name: 'Sony ZV-E10 II', imageUrl: null, price: 21_000_000, currency: 'VND', priceVaries: false },
  ],
  categories: [{ id: 'c1', name: 'Mirrorless cameras' }],
}

function Where() {
  const location = useLocation()
  return <p data-testid="where">{location.pathname + location.search}</p>
}

function renderBox(onSubmit = vi.fn((event: React.FormEvent) => event.preventDefault())) {
  renderSignedOut(
    <>
      <form onSubmit={onSubmit}>
        <SearchBox initial="" />
      </form>
      <Routes>
        <Route path="*" element={<Where />} />
      </Routes>
    </>,
  )
  return onSubmit
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('SearchBox (specs/164)', () => {
  it('suggests products and categories once typing pauses', async () => {
    const suggest = vi.spyOn(Product, 'suggest').mockResolvedValue(answer)
    const user = userEvent.setup()
    renderBox()

    await user.type(screen.getByRole('combobox', { name: 'Search' }), 'sony')

    const options = await screen.findAllByRole('option')
    expect(options.map((o) => o.textContent)).toEqual([
      expect.stringContaining('Sony Alpha A7 IV'),
      expect.stringContaining('Sony ZV-E10 II'),
      expect.stringContaining('Mirrorless cameras'),
    ])
    // Once for the paused term, not once per key.
    expect(suggest).toHaveBeenCalledTimes(1)
    expect(suggest).toHaveBeenCalledWith('sony')
    expect(screen.getByRole('combobox')).toHaveAttribute('aria-expanded', 'true')
  })

  it('asks nothing for a single character', async () => {
    const suggest = vi.spyOn(Product, 'suggest').mockResolvedValue(answer)
    const user = userEvent.setup()
    renderBox()

    await user.type(screen.getByRole('combobox'), 's')
    await new Promise((resolve) => setTimeout(resolve, 300))

    expect(suggest).not.toHaveBeenCalled()
    expect(screen.queryByRole('listbox')).toBeNull()
  })

  it('opens the option the keys highlight', async () => {
    vi.spyOn(Product, 'suggest').mockResolvedValue(answer)
    const user = userEvent.setup()
    const submit = renderBox()

    await user.type(screen.getByRole('combobox'), 'sony')
    await screen.findAllByRole('option')
    await user.keyboard('{ArrowDown}{ArrowDown}')
    expect(screen.getByRole('combobox')).toHaveAttribute('aria-activedescendant', screen.getAllByRole('option')[1].id)
    await user.keyboard('{Enter}')

    expect(screen.getByTestId('where')).toHaveTextContent('/products/p2')
    expect(submit).not.toHaveBeenCalled()
  })

  it('opens a category as the listing filtered to it', async () => {
    vi.spyOn(Product, 'suggest').mockResolvedValue(answer)
    const user = userEvent.setup()
    renderBox()

    await user.type(screen.getByRole('combobox'), 'sony')
    await user.click(await screen.findByRole('option', { name: /Mirrorless cameras/ }))

    expect(screen.getByTestId('where')).toHaveTextContent('/?category=c1')
  })

  it('searches as before on Enter with nothing highlighted, and Escape closes', async () => {
    vi.spyOn(Product, 'suggest').mockResolvedValue(answer)
    const user = userEvent.setup()
    const submit = renderBox()

    await user.type(screen.getByRole('combobox'), 'sony')
    await screen.findAllByRole('option')
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('listbox')).toBeNull()

    await user.keyboard('{Enter}')
    await waitFor(() => expect(submit).toHaveBeenCalledTimes(1))
    expect(screen.getByTestId('where')).toHaveTextContent('/')
  })

  /** Found in a browser: the search ran and its results sat under a dropdown still open. */
  it('closes the suggestions when Enter searches', async () => {
    vi.spyOn(Product, 'suggest').mockResolvedValue(answer)
    const user = userEvent.setup()
    const submit = renderBox()

    await user.type(screen.getByRole('combobox'), 'sony')
    await screen.findAllByRole('option')
    await user.keyboard('{Enter}')

    expect(submit).toHaveBeenCalledTimes(1)
    expect(screen.queryByRole('listbox')).toBeNull()
  })
})
