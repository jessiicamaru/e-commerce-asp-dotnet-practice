// Customers check out at a steady rate (specs/144, US2): add to cart, ask for a quote, place the order, follow it to
// Paid - at a constant arrival rate, so the stack is offered the same load however fast it answers. Each customer is
// one VU at a time, so no two checkouts share a cart. Plenty of stock: this measures the path, not the race.
import { Counter } from 'k6/metrics'
import { checkConsistency, checkOut, customers, listProduct, removeProduct, settle, signInAsAdmin, summary, reporting } from './lib/shop.js'

const RATE = Number(__ENV.RATE || 5)             // checkouts started per second
const DURATION = __ENV.DURATION || '60s'
const PEOPLE = Number(__ENV.PEOPLE || 60)         // also the most VUs: one customer each
const STOCK = 100000
const checkoutErrors = new Counter('checkout_errors')

export const options = {
  setupTimeout: '15m',
  teardownTimeout: '15m',
  scenarios: {
    checkout: {
      executor: 'constant-arrival-rate', rate: RATE, timeUnit: '1s', duration: DURATION,
      preAllocatedVUs: PEOPLE, maxVUs: PEOPLE,
    },
  },
  ...reporting(['add to cart', 'quote', 'place order', 'read order'], {
    broken_invariants: ['count==0'],
    checkout_errors: ['count==0'],
    http_req_failed: ['rate==0'],
  }),
}

export function setup() {
  const admin = signInAsAdmin()
  return { admin, listed: listProduct(admin, 'checkout', 990000, STOCK), people: customers(PEOPLE, 'checkout') }
}

export default function (data) {
  const person = data.people[(__VU - 1) % data.people.length]
  try {
    const order = checkOut(person, data.listed.product)
    if (settle(person, order) === null) checkoutErrors.add(1)
  } catch (error) {
    console.error(String(error))
    checkoutErrors.add(1)
  }
}

export function teardown(data) {
  checkConsistency(data.listed, data.people)
  removeProduct(data.admin, data.listed)
}

export const handleSummary = summary('checkout', { rate_per_second: RATE, duration: DURATION, customers: PEOPLE, executor: 'constant-arrival-rate' })
