import { afterEach, describe, expect, it, vi } from 'vitest'
import { openNoticeLink } from './open-link'

afterEach(() => vi.unstubAllGlobals())

describe('openNoticeLink (specs/137)', () => {
  it('opens a console link in the back office', () => {
    const assign = vi.fn()
    vi.stubGlobal('location', { ...window.location, assign })
    const navigate = vi.fn()

    openNoticeLink('/admin/email-delivery', navigate)

    expect(assign).toHaveBeenCalledWith('http://portal.localhost:5174/email-delivery')
    expect(navigate).not.toHaveBeenCalled()
  })

  it('opens any other link in this app', () => {
    const navigate = vi.fn()
    openNoticeLink('/orders/o1', navigate)
    expect(navigate).toHaveBeenCalledWith('/orders/o1')
  })
})
