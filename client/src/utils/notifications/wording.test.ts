import { afterEach, describe, expect, it } from 'vitest'
import i18n from '@/config/i18n'
import { oneLine } from '@/components/shared/rich-text-editor/one-line'
import { applyWording, BUNDLED_WORDING, fillSample } from './wording'

describe('the notices wording (specs/078)', () => {
  afterEach(() => applyWording(i18n, {}))

  it('lays an edit over the bundled words, in its language only', () => {
    applyWording(i18n, { en: { NewSale: 'A <strong>new sale</strong>: {{order}}' } })

    expect(i18n.getFixedT('en', 'notifications')('kind.NewSale', { order: '01a0dd2b' })).toBe('A <strong>new sale</strong>: 01a0dd2b')
    expect(i18n.getFixedT('vi', 'notifications')('kind.NewSale', { order: '01a0dd2b' })).toBe('Bạn có đơn bán mới: 01a0dd2b.')
  })

  /** A reset on the server means the key is no longer edited: the bundle comes back, not the stale edit. */
  it('goes back to the bundled words once an edit is gone', () => {
    applyWording(i18n, { en: { NewSale: 'Edited' } })
    applyWording(i18n, { en: {} })

    expect(i18n.getResource('en', 'notifications', 'kind.NewSale')).toBe(BUNDLED_WORDING.en.NewSale)
  })

  it('fills a draft with made-up values, and leaves an unknown placeholder visible', () => {
    expect(fillSample('{{product}} is on sale {{ unknown }}', { product: 'X-T5' })).toBe('X-T5 is on sale {{ unknown }}')
  })

  it('keeps a notice to one line when the editor wraps it in paragraphs', () => {
    expect(oneLine('<p>First <strong>line</strong></p><p>second</p>')).toBe('First <strong>line</strong> second')
  })
})
