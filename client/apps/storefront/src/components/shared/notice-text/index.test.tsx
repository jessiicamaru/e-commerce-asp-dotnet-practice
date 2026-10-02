import { render } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { NoticeText } from '.'

const html = (markup: string, links = false) => render(<NoticeText html={markup} links={links} />).container.innerHTML

describe('NoticeText (specs/078)', () => {
  /** Sanitised here again, whatever the server did: this is what runs in the reader's page. */
  it('keeps emphasis and drops what could run', () => {
    const shown = html('Paid <strong>01a0dd2b</strong><script>alert(1)</script><img src=x onerror="steal()"><em onclick="x()">now</em>')
    expect(shown).toContain('<strong>01a0dd2b</strong>')
    expect(shown).toContain('<em>now</em>')
    expect(shown).not.toMatch(/script|img|onerror|onclick/)
  })

  it('shows a link as its words inside something already clickable, and as a link elsewhere', () => {
    expect(html('See <a href="/orders">your orders</a>')).not.toContain('<a')
    expect(html('See <a href="/orders">your orders</a>')).toContain('your orders')
    expect(html('See <a href="/orders">your orders</a>', true)).toContain('<a href="/orders">your orders</a>')
  })

  it('keeps links on the web or the shop only', () => {
    expect(html('<a href="https://shop.example/x">a</a>', true)).toContain('href="https://shop.example/x"')
    expect(html('<a href="javascript:alert(1)">b</a>', true)).not.toContain('javascript')
    expect(html('<a href="//evil.test/x">c</a>', true)).not.toContain('evil.test')
  })
})
