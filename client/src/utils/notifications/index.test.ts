import { beforeEach, describe, expect, it } from 'vitest'
import i18n from '@/config/i18n'
import type { AppNotification } from '@/services/notifications/types'
import { describeNotification } from '.'

const n = (kind: string, data: Record<string, string> = {}): AppNotification => ({
  id: 'n', kind, data: { orderId: '01a0cee7-137c-7bbc', ...data }, link: null, createdAt: '', readAt: null,
})
const t = (lang: string) => i18n.getFixedT(lang, 'notifications')

describe('describeNotification (specs/042)', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en')
  })

  it('words each kind from its data, in the reader language', () => {
    expect(describeNotification(t('en'), n('OrderPaid', { total: '22462000', currency: 'VND' })))
      .toBe('Order 01a0cee7 is paid (₫22,462,000). We will prepare it soon.')
    expect(describeNotification(t('en'), n('ParcelShipped', { tracking: 'VN-1', shop: 'Mai Lens' })))
      .toContain('from Mai Lens is on its way - tracking VN-1')
    expect(describeNotification(t('vi'), n('NewSale'))).toBe('Bạn có đơn bán mới: 01a0cee7.')
  })

  /** The same notice, stored once, reads in whichever language the reader has now (research D1). */
  it('says who cancelled in words, not the stored code', () => {
    expect(describeNotification(t('en'), n('OrderCancelled', { by: 'Customer' }))).toContain('cancelled by you')
    expect(describeNotification(t('vi'), n('OrderCancelled', { by: 'Staff' }))).toContain('bởi cửa hàng')
  })

  it('names the shop itself when a parcel has no seller', () => {
    expect(describeNotification(t('en'), n('ParcelShipped', { tracking: 'VN-2' }))).toContain('from the shop')
  })

  /** A newer service may send a kind this storefront has no words for yet: still shown, never hidden. */
  it('still shows a kind it does not know', () => {
    expect(describeNotification(t('en'), n('SomethingNew'))).toBe('You have a new update.')
  })
})
