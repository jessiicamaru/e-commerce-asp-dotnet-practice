import { beforeEach, describe, expect, it } from 'vitest'
import i18n from '@/config/i18n'
import type { AppNotification } from '@/services/notifications/types'
import en from '@/locales/en/notifications.json'
import vi from '@/locales/vi/notifications.json'
// What the services actually send (specs/048): the server's tests check every notice they publish against
// this same file, so a key renamed on one side and not the other goes red somewhere.
import declared from '../../../../server/src/BuildingBlocks/Ecommerce.Shared/Notifications/notification-kinds.json'
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

/** The five kinds that read `“{{product}}” was not approved: {{reason}}` until #119. */
describe('the moderation and review kinds (#119)', () => {
  it('says which shop was refused and why', () => {
    expect(describeNotification(t('en'), n('ShopRejected', { shop: 'Mai Lens', reason: 'no address' })))
      .toBe('Your shop “Mai Lens” was not approved: no address')
    expect(describeNotification(t('vi'), n('ShopRejected', { shop: 'Mai Lens', reason: 'thiếu địa chỉ' })))
      .toBe('Gian hàng “Mai Lens” không được duyệt: thiếu địa chỉ')
  })

  it('names the approved product', () => {
    expect(describeNotification(t('en'), n('ProductApproved', { product: 'Fujifilm X-T5' })))
      .toBe('“Fujifilm X-T5” is approved and on sale.')
  })

  it('says which product was refused and why', () => {
    expect(describeNotification(t('en'), n('ProductRejected', { product: 'Fujifilm X-T5', reason: 'blurred photos' })))
      .toBe('“Fujifilm X-T5” was not approved: blurred photos')
  })

  it('says which product was taken down and why', () => {
    expect(describeNotification(t('vi'), n('ProductTakenDown', { product: 'Sony A7 IV', reason: 'hàng giả' })))
      .toBe('“Sony A7 IV” đã bị gỡ khỏi kệ: hàng giả')
  })

  it('counts the stars of a review, one or many', () => {
    expect(describeNotification(t('en'), n('NewReview', { product: 'Sony A7 IV', rating: '1' })))
      .toBe('Somebody gave “Sony A7 IV” 1 star.')
    expect(describeNotification(t('en'), n('NewReview', { product: 'Sony A7 IV', rating: '5' })))
      .toBe('Somebody gave “Sony A7 IV” 5 stars.')
    expect(describeNotification(t('vi'), n('NewReview', { product: 'Sony A7 IV', rating: '4' })))
      .toBe('Có người chấm “Sony A7 IV” 4 sao.')
  })

  /** specs/059: the end of a lock is a moment in the reader's own language and time, never raw UTC. */
  it('words a lock with its end in the reader language and the reason', () => {
    const until = '2026-10-01T07:30:00Z'
    const en = describeNotification(t('en'), n('AccountLocked', { until, reason: 'Spam in reviews' }), 'en')
    expect(en).toContain(new Date(until).toLocaleString('en'))
    expect(en).toContain('Spam in reviews')
    expect(en).not.toContain('07:30:00Z')
    expect(describeNotification(t('vi'), n('AccountLocked', { until, reason: 'Spam' }), 'vi'))
      .toContain(new Date(until).toLocaleString('vi'))
  })

  /** A sentence with a hole in it is worse than a vaguer one (spec FR-003). */
  it('falls back to the generic sentence rather than show a hole', () => {
    expect(describeNotification(t('en'), { ...n('ProductRejected', { product: 'X' }) })).toBe('You have a new update.')
    expect(describeNotification(t('en'), { ...n('ShopApproved'), data: {} })).toBe('You have a new update.')
  })
})

type Declaration = { kinds: Record<string, { required: string[]; optional?: string[] }> }
const kinds = (declared as Declaration).kinds

/**
 * A value per key a service can send, and how it must show up in the sentence. A key the server starts
 * sending that is not listed here fails the test below - somebody has to decide how it reads.
 */
const samples: Record<string, { value: string; shows: string | null }> = {
  orderId: { value: '0199aa11-2233-7bbc-8ddd-eeeeffff0000', shows: '0199aa11' },
  total: { value: '1250000', shows: '1,250,000' },
  amount: { value: '980000', shows: '980,000' },
  currency: { value: 'VND', shows: null }, // formats total and amount; never shown alone
  tracking: { value: 'VN-TRACK-42', shows: 'VN-TRACK-42' },
  shop: { value: 'Sample Shop', shows: 'Sample Shop' },
  by: { value: 'Customer', shows: null }, // worded as "you" / "bạn", asserted above
  product: { value: 'Sample Product', shows: 'Sample Product' },
  reason: { value: 'Sample reason', shows: 'Sample reason' },
  rating: { value: '4', shows: '4' },
  until: { value: '2026-10-01T07:30:00Z', shows: '2026' },   // formatted in the reader's language (specs/059)
}

describe('every kind a service can send (specs/048)', () => {
  const langs = ['en', 'vi'] as const
  const sentences = { en: en.kind as Record<string, string>, vi: vi.kind as Record<string, string> }

  it('has words, in both languages, for exactly the kinds the services declare', () => {
    for (const lang of langs) {
      const worded = new Set(Object.keys(sentences[lang]).map((k) => k.replace(/_(one|other)$/, '')))
      expect([...worded].sort(), lang).toEqual(Object.keys(kinds).sort())
    }
  })

  for (const [kind, keys] of Object.entries(kinds)) {
    for (const lang of langs) {
      it(`${kind} reads as a sentence in ${lang}, showing what it was sent`, () => {
        const sent = [...keys.required, ...(keys.optional ?? [])]
        const data = Object.fromEntries(sent.map((key) => {
          expect(samples[key], `no sample for "${key}" - decide how it reads`).toBeDefined()
          return [key, samples[key].value]
        }))
        const text = describeNotification(t(lang), { ...n(kind), data }, lang)

        expect(text).not.toContain('{{')
        expect(text).not.toBe(t(lang)('generic'))
        for (const key of sent) {
          if (samples[key].shows) expect(text, key).toContain(samples[key].shows)
        }
      })
    }
  }
})
