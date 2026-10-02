import { afterEach, describe, expect, it, vi } from 'vitest'
import { Auth } from '@ecommerce/core/services/auth'
import { goToBackOffice } from '.'

afterEach(() => vi.unstubAllGlobals())

describe('goToBackOffice (specs/140)', () => {
  it('carries a handoff in the fragment, where no server sees it', async () => {
    const assign = vi.fn()
    vi.stubGlobal('location', { ...window.location, assign })
    vi.spyOn(Auth, 'handoff').mockResolvedValue('abc-123_X')

    await goToBackOffice()

    expect(assign).toHaveBeenCalledWith('http://portal.localhost:5174/auth/callback#code=abc-123_X')
  })

  it('opens the back office anyway when no handoff can be had', async () => {
    const assign = vi.fn()
    vi.stubGlobal('location', { ...window.location, assign })
    vi.spyOn(Auth, 'handoff').mockRejectedValue(new Error('403'))

    await goToBackOffice()

    expect(assign).toHaveBeenCalledWith('http://portal.localhost:5174/')
  })
})
