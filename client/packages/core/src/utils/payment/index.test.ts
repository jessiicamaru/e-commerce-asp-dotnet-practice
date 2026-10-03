import { describe, expect, it } from 'vitest'
import { orderIdFromReference } from '.'

describe('orderIdFromReference (specs/143)', () => {
  it("turns VNPay's reference back into the order id", () => {
    expect(orderIdFromReference('0199A1B2C3D4E5F60718293A4B5C6D7E')).toBe('0199a1b2-c3d4-e5f6-0718-293a4b5c6d7e')
  })

  it('names no order for anything that is not a reference of this shop', () => {
    for (const reference of [null, undefined, '', '0199a1b2', '0199a1b2-c3d4-e5f6-0718-293a4b5c6d7e', 'zz99a1b2c3d4e5f60718293a4b5c6d7e', '../../admin'])
      expect(orderIdFromReference(reference)).toBeNull()
  })
})
