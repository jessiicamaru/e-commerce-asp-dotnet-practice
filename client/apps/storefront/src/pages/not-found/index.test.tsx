import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { NotFoundPage } from '.'

function Where() {
  const location = useLocation()
  return <p data-testid="where">{location.pathname + location.search}</p>
}

function renderAt(path: string) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/" element={<Where />} />
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </MemoryRouter>,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('NotFoundPage (specs/120, #243)', () => {
  it('says what happened and offers a way back', () => {
    renderAt('/no-such-page')

    expect(screen.getByRole('heading', { name: 'This page is not here' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to the shop' })).toHaveAttribute('href', '/')
    expect(screen.getByRole('searchbox', { name: 'Search the shop' })).toBeInTheDocument()
  })

  it('is Vietnamese for a Vietnamese reader', async () => {
    await i18n.changeLanguage('vi')
    renderAt('/khong-co')

    expect(screen.getByRole('heading', { name: 'Không có trang này' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Về cửa hàng' })).toBeInTheDocument()
    expect(screen.queryByText(/not found|not here|Back to the shop/i)).not.toBeInTheDocument()
  })

  it('searches the catalogue the way the header does', async () => {
    renderAt('/old-link')

    await userEvent.type(screen.getByRole('searchbox'), '  canon r50 ')
    await userEvent.click(screen.getByRole('button', { name: 'Search' }))

    expect(screen.getByTestId('where')).toHaveTextContent('/?q=canon%20r50')
  })
})
