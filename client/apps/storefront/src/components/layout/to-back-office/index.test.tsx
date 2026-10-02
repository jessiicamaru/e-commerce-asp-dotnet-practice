import { render } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ToBackOffice } from '.'

afterEach(() => vi.unstubAllGlobals())

describe('ToBackOffice (specs/137)', () => {
  it('sends an old console address to the same page in the back office', () => {
    const replace = vi.fn()
    vi.stubGlobal('location', { ...window.location, replace })

    render(
      <MemoryRouter initialEntries={['/admin/users?role=Moderator']}>
        <Routes>
          <Route path="/admin/*" element={<ToBackOffice />} />
        </Routes>
      </MemoryRouter>,
    )

    expect(replace).toHaveBeenCalledWith('http://portal.localhost:5174/users?role=Moderator')
  })
})
