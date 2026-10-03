// Checkouts through a fault (specs/147, #291): customers check out at a steady rate while fault.sh takes something
// away and brings it back. An iteration only places its order - waiting would pile VUs up during an outage and stop
// the load being what it says (research D2). The teardown waits for every order to settle, as long as SETTLE_TIMEOUT,
// then checks that nothing was lost: every order terminal, units sold = units deducted, nothing held.
import http from 'k6/http'
import exec from 'k6/execution'
import { Counter } from 'k6/metrics'
import { GATEWAY, checkConsistency, checkOut, customers, listProduct, removeProduct, reporting, signInAsAdmin, summary } from './lib/shop.js'

const RATE = Number(__ENV.RATE || 3)
const DURATION = __ENV.LOAD_FOR || '120s'
const PEOPLE = Number(__ENV.PEOPLE || 40)
const STOCK = 100000
const FAULT = __ENV.FAULT || 'none'

// What customers saw while something was down: counted and reported, not a threshold (research D4).
const checkoutErrors = new Counter('checkout_errors')
const placed = new Counter('orders_placed')

export const options = {
  setupTimeout: '15m',
  teardownTimeout: '20m',
  scenarios: {
    resilience: {
      executor: 'constant-arrival-rate', rate: RATE, timeUnit: '1s', duration: DURATION,
      preAllocatedVUs: PEOPLE, maxVUs: PEOPLE,
    },
  },
  // Only what must never happen fails the run: an order lost, stuck, or stock that disagrees.
  ...reporting(['add to cart', 'quote', 'place order'], { broken_invariants: ['count==0'] }),
}

export function setup() {
  const admin = signInAsAdmin()
  return { admin, listed: listProduct(admin, `resilience-${FAULT}`, 990000, STOCK), people: customers(PEOPLE, `res-${FAULT}`) }
}

export default function (data) {
  // Customers in turn, by iteration: at 3 a second, 40 customers come round every 13 s, never two checkouts at once.
  const person = data.people[exec.scenario.iterationInTest % data.people.length]
  try {
    // An iteration does not wait for its order, so this customer's previous order may still be in flight - and Cart
    // removes lines only when an order completes (specs/010). Emptying the cart first keeps every order one unit.
    http.del(`${GATEWAY}/api/cart`, null, { headers: { Authorization: `Bearer ${person.token}` }, tags: { name: 'clear cart' } })
    checkOut(person, data.listed.product)
    placed.add(1)
  } catch (error) {
    console.warn(`checkout refused while ${FAULT}: ${String(error).slice(0, 160)}`)
    checkoutErrors.add(1)
  }
}

/**
 * Every order's placed and paid times, from Order's own record, for the driver to set against its fault timeline: what
 * was placed during the fault, and how long after recovery that backlog cleared (research D3).
 */
function timings(people) {
  const pairs = []
  for (const person of people) {
    const auth = { headers: { Authorization: `Bearer ${person.token}` }, tags: { name: 'timings' } }
    const page = http.get(`${GATEWAY}/api/orders?page=1&pageSize=100`, auth).json()
    for (const row of page.items || []) {
      const order = http.get(`${GATEWAY}/api/orders/${row.orderId}`, auth).json()
      pairs.push([Date.parse(order.createdAt), order.paidAt ? Date.parse(order.paidAt) : null])
    }
  }
  return pairs
}

export function teardown(data) {
  checkConsistency(data.listed, data.people)
  console.log(`timings: ${JSON.stringify(timings(data.people))}`)
  removeProduct(data.admin, data.listed)
}

export const handleSummary = summary(`resilience-${FAULT}`, { fault: FAULT, rate_per_second: RATE, load_for: DURATION, customers: PEOPLE })
