import { describe, expect, it } from 'vitest'
import type { MyDataService, ServiceExport } from '@ecommerce/core/services/my-data/types'
import { composeMyData, myDataFileName } from './my-data'

const answer = (service: string): PromiseSettledResult<ServiceExport> => ({
  status: 'fulfilled',
  value: { service, exportedAt: '2026-10-01T00:00:00Z', sections: { rows: [{ service }] }, withheld: [] },
})
const down: PromiseSettledResult<ServiceExport> = { status: 'rejected', reason: new Error('503') }
const all = (overrides: Partial<Record<MyDataService, PromiseSettledResult<ServiceExport>>> = {}) => ({
  identity: answer('identity'),
  catalog: answer('catalog'),
  order: answer('order'),
  cart: answer('cart'),
  payment: answer('payment'),
  activity: answer('activity'),
  ...overrides,
})
const person = { id: 'p-1', email: 'mai@demo.test' }

describe('composeMyData (specs/111)', () => {
  it('puts every answer under its service, with who and when', () => {
    const now = new Date('2026-10-01T08:30:00Z')
    const { file, unavailable } = composeMyData(person, all(), now)

    expect(unavailable).toEqual([])
    expect(file.exportedAt).toBe('2026-10-01T08:30:00.000Z')
    expect(file.person).toEqual(person)
    expect(Object.keys(file.services)).toEqual(['identity', 'catalog', 'order', 'cart', 'payment', 'activity'])
    expect(file.services.order).toMatchObject({ service: 'order' })
  })

  it('marks a service that did not answer rather than leaving it out, and names it', () => {
    const { file, unavailable } = composeMyData(person, all({ payment: down, cart: down }), new Date())

    expect(file.services.payment).toEqual({ unavailable: true })
    expect(file.services.cart).toEqual({ unavailable: true })
    expect(file.services.identity).toMatchObject({ service: 'identity' })
    expect(unavailable).toEqual(['cart', 'payment'])
  })
})

describe('myDataFileName', () => {
  it('names the file by the local day, zero-padded', () => {
    expect(myDataFileName(new Date(2026, 0, 5, 23, 59))).toBe('my-data-2026-01-05.json')
  })
})
