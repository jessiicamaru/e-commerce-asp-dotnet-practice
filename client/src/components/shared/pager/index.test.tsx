import { fireEvent, render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Pager } from '.'
import { pageWindow } from './page-window'

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('pageWindow', () => {
  it('shows every page when there are few', () => {
    expect(pageWindow(2, 5)).toEqual([1, 2, 3, 4, 5])
  })

  it('keeps the first, the last and the neighbours, with gaps between', () => {
    expect(pageWindow(6, 12)).toEqual([1, '…', 5, 6, 7, '…', 12])
  })

  /** Hiding a single page behind "…" takes the same room as showing it, and hides it for nothing. */
  it('never hides just one page behind a gap', () => {
    expect(pageWindow(5, 12)).toEqual([1, '…', 4, 5, 6, '…', 12])
    expect(pageWindow(4, 12).slice(0, 5)).toEqual([1, 2, 3, 4, 5])
  })

  it('keeps five in a row at either end, so the bar does not change width', () => {
    expect(pageWindow(1, 12)).toEqual([1, 2, 3, 4, 5, '…', 12])
    expect(pageWindow(12, 12)).toEqual([1, '…', 8, 9, 10, 11, 12])
  })
})

describe('Pager', () => {
  it('says which rows are on screen', () => {
    render(<Pager page={2} pageSize={12} totalCount={30} onChange={() => {}} />)

    expect(screen.getByText('Showing 13–24 of 30')).toBeInTheDocument()
  })

  it('asks for the page that was clicked, and keeps the click from reloading', () => {
    const onChange = vi.fn()
    render(<Pager page={1} pageSize={12} totalCount={30} onChange={onChange} />)

    fireEvent.click(screen.getByRole('button', { name: 'Page 3' }))
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))

    expect(onChange.mock.calls).toEqual([[3], [2]])
  })

  it('does not go before the first page', () => {
    const onChange = vi.fn()
    render(<Pager page={1} pageSize={12} totalCount={30} onChange={onChange} />)

    fireEvent.click(screen.getByRole('button', { name: 'Previous' }))

    expect(onChange).not.toHaveBeenCalled()
  })

  it('draws no page numbers when everything fits on one page', () => {
    render(<Pager page={1} pageSize={12} totalCount={7} onChange={() => {}} />)

    expect(screen.getByText('Showing 1–7 of 7')).toBeInTheDocument()
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument()
  })
})
