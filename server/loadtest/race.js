// Many customers race for the last units (specs/144, US1): 100 customers each check out one unit of a variant with 20,
// all at once. Exactly 20 may be paid; the rest must fail for stock, with nothing held and nothing stuck. This is the
// row lock of specs/001, through the gateway, the saga and five services at the same moment.
import exec from 'k6/execution'
import { Counter } from 'k6/metrics'
import { checkConsistency, checkOut, customers, listProduct, removeProduct, settle, signInAsAdmin, summary, reporting } from './lib/shop.js'

const STOCK = Number(__ENV.STOCK || 20)
const PEOPLE = Number(__ENV.PEOPLE || 100)
const checkoutErrors = new Counter('checkout_errors')

export const options = {
  setupTimeout: '15m',
  teardownTimeout: '15m',
  scenarios: {
    race: { executor: 'shared-iterations', vus: PEOPLE, iterations: PEOPLE, maxDuration: '10m' },
  },
  ...reporting(['add to cart', 'quote', 'place order', 'read order'], {
    // Correctness, not speed (research D5): a broken invariant or an unexpected answer fails the run.
    broken_invariants: ['count==0'],
    checkout_errors: ['count==0'],
    http_req_failed: ['rate==0'],
  }),
}

export function setup() {
  const admin = signInAsAdmin()
  return { admin, listed: listProduct(admin, 'race', 990000, STOCK), people: customers(PEOPLE, 'race') }
}

export default function (data) {
  // One customer per iteration, so no two iterations share a cart.
  const person = data.people[exec.scenario.iterationInTest]
  try {
    const order = checkOut(person, data.listed.product)
    settle(person, order)
  } catch (error) {
    console.error(String(error))
    checkoutErrors.add(1)
  }
}

export function teardown(data) {
  checkConsistency(data.listed, data.people, STOCK)
  removeProduct(data.admin, data.listed)
}

export const handleSummary = summary('race', { customers: PEOPLE, stock: STOCK, executor: 'shared-iterations, every customer at once' })
