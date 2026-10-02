import { beforeEach, describe, expect, it } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { refusal } from '@ecommerce/core/test/refusal'
import { deletionRefusal } from './deletion'

const t = i18n.getFixedT('en', 'auth')

describe('deletionRefusal (specs/112)', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en')
  })

  it('words each open reason in the reader language', () => {
    const lines = deletionRefusal(
      refusal(409, 'Business is open.', { code: 'AccountHasOpenBusiness', reasons: ['OpenOrders', 'UnpaidEarnings'] }),
      t,
    )

    expect(lines).toEqual([t('account.deleteBlockers.OpenOrders'), t('account.deleteBlockers.UnpaidEarnings')])
  })

  it('keeps the server sentence when a reason is one this page cannot word', () => {
    const lines = deletionRefusal(
      refusal(409, 'Business is open.', { code: 'AccountHasOpenBusiness', reasons: ['OpenReturns', 'SomethingNew'] }),
      t,
    )

    expect(lines).toEqual([t('account.deleteBlockers.OpenReturns'), 'Business is open.'])
  })

  it('says the staff rule for a staff account', () => {
    expect(deletionRefusal(refusal(409, 'A staff account...', { code: 'StaffAccount' }), t)).toEqual([t('account.deleteStaff')])
  })

  it('leaves anything but a 409 to the rest of the page', () => {
    expect(deletionRefusal(refusal(400, 'Your current password is not correct.'), t)).toBeNull()
    expect(deletionRefusal(refusal(503, 'Order is down.'), t)).toBeNull()
  })
})
