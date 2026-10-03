// What every load scenario needs from the shop (specs/144): staff sign-in, a product to sell, customers, a checkout, and
// the check that the stock and the orders still agree afterwards. Every measured request goes through the gateway;
// setup reaches Identity directly only because the gateway rightly limits registrations per address (specs/062).
import http from 'k6/http'
import crypto from 'k6/crypto'
import encoding from 'k6/encoding'
import { sleep } from 'k6'
import { Counter, Gauge, Trend } from 'k6/metrics'

export const GATEWAY = __ENV.GATEWAY_URL || 'http://gateway:8080'
export const IDENTITY = __ENV.IDENTITY_URL || 'http://identity:8080'
const BACK_OFFICE_ORIGIN = __ENV.BACK_OFFICE_ORIGIN || 'http://portal.localhost:8089'
const PASSWORD = 'Load-Passw0rd!1'

/** Broken invariants: a threshold of zero turns any of them into a failed run. */
export const brokenInvariants = new Counter('broken_invariants')
/** Placing an order to its saga's outcome (Paid or Failed), in milliseconds. */
export const settleTime = new Trend('order_settle_ms', true)

// A JSON request's body and parameters, spread into http.post/put: k6 takes the body as its own argument.
const json = (body) => [JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }]
const bearer = (token, extra = {}) => ({ headers: { Authorization: `Bearer ${token}`, ...extra } })
const withJson = (token, body, tags = {}) => [
  JSON.stringify(body),
  { headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' }, tags },
]

function must(response, what, ...okStatuses) {
  if (!okStatuses.includes(response.status)) {
    throw new Error(`${what}: HTTP ${response.status} ${String(response.body).slice(0, 300)}`)
  }
  return response
}

// --- staff sign-in (specs/110, specs/138) ---------------------------------------------------------------------------

function base32(secret) {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'
  const clean = secret.replace(/=+$/, '').replace(/\s/g, '').toUpperCase()
  let bits = ''
  for (const c of clean) bits += alphabet.indexOf(c).toString(2).padStart(5, '0')
  const bytes = new Uint8Array(Math.floor(bits.length / 8))
  for (let i = 0; i < bytes.length; i++) bytes[i] = parseInt(bits.slice(i * 8, i * 8 + 8), 2)
  return bytes.buffer
}

/** RFC 6238: HMAC-SHA1 over the 30-second step, six digits. */
export function totp(secret, step = Math.floor(Date.now() / 1000 / 30)) {
  const counter = new Uint8Array(8)
  let s = step
  for (let i = 7; i >= 0; i--) { counter[i] = s & 0xff; s = Math.floor(s / 256) }
  const hmac = crypto.createHMAC('sha1', base32(secret))
  hmac.update(counter.buffer)
  const h = new Uint8Array(encoding.b64decode(hmac.digest('base64'), 'std'))
  const o = h[h.length - 1] & 15
  const n = ((h[o] & 0x7f) << 24) | (h[o + 1] << 16) | (h[o + 2] << 8) | h[o + 3]
  return String(n % 1_000_000).padStart(6, '0')
}

/** The administrator, signed in as the back office does: password, then the code. A used code waits for the next window. */
export function signInAsAdmin() {
  for (let attempt = 0; attempt < 2; attempt++) {
    const first = must(http.post(`${IDENTITY}/api/auth/login`, JSON.stringify({ email: __ENV.ADMIN_EMAIL, password: __ENV.ADMIN_PASSWORD }),
      { headers: { 'Content-Type': 'application/json' } }), 'admin sign-in', 200).json()
    const answer = http.post(`${IDENTITY}/api/auth/login/two-factor`,
      JSON.stringify({ challenge: first.challenge, code: totp(__ENV.ADMIN_TOTP_SECRET) }),
      { headers: { 'Content-Type': 'application/json', Origin: BACK_OFFICE_ORIGIN } })
    if (answer.status === 200) return answer.json().token
    sleep(31 - (Math.floor(Date.now() / 1000) % 30))
  }
  throw new Error('the administrator could not sign in with a code - is ADMIN_TOTP_SECRET the seeded one?')
}

// --- the catalogue --------------------------------------------------------------------------------------------------

/** A category and one product of the shop's own (no review needed), with this much stock. */
export function listProduct(admin, label, price, stock) {
  const run = `${Date.now().toString(36)}${Math.floor(Math.random() * 1e6).toString(36)}`
  const category = must(http.post(`${GATEWAY}/api/categories`, ...withJson(admin,
    { name: `Load ${label} ${run}`, description: 'created by a load test (specs/144)', slug: `load-${label}-${run}` })),
    'create category', 200, 201).json().id
  const product = must(http.post(`${GATEWAY}/api/products`, ...withJson(admin,
    { name: `Load ${label} ${run}`, description: 'created by a load test (specs/144)', price, sku: `LOAD${run}`.toUpperCase(), categoryId: category })),
    'create product', 200, 201).json().id
  // The first variant reuses the product's id; Inventory learns of it from an event, so wait for the row.
  // A 404 here is the row not having arrived yet, not a failure (it would count against http_req_failed otherwise).
  const notYet = { responseCallback: http.expectedStatuses(200, 404) }
  for (let i = 0; i < 60 && http.get(`${GATEWAY}/api/stock/${product}`, notYet).status !== 200; i++) sleep(1)
  must(http.put(`${GATEWAY}/api/stock/${product}`, ...withJson(admin, { quantityOnHand: stock })), 'set stock', 200, 204)
  // And for the catalogue to say it is in stock, or checkout would refuse a product it thinks is out.
  for (let i = 0; i < 60; i++) {
    const p = http.get(`${GATEWAY}/api/products/${product}`)
    if (p.status === 200 && p.json().availability === 'InStock') break
    sleep(1)
  }
  return { category, product, stock }
}

export function readStock(variant) {
  return must(http.get(`${GATEWAY}/api/stock/${variant}`), 'read stock', 200).json()
}

export function removeProduct(admin, listed) {
  http.del(`${GATEWAY}/api/products/${listed.product}`, null, bearer(admin))
  http.del(`${GATEWAY}/api/categories/${listed.category}`, null, bearer(admin))
}

// --- customers ------------------------------------------------------------------------------------------------------

/** Registered through Identity directly (D2), each with a delivery address. */
export function customers(count, label) {
  const run = Date.now().toString(36)
  const made = []
  for (let i = 0; i < count; i++) {
    const email = `load-${label}-${run}-${i}@load.test`
    const token = must(http.post(`${IDENTITY}/api/auth/register`,
      ...json({ email, password: PASSWORD, firstName: 'Load', lastName: `Customer ${i}` })), `register ${email}`, 200).json().token
    const address = must(http.post(`${IDENTITY}/api/addresses`, ...withJson(token,
      { recipientName: `Load ${i}`, line1: '1 Load Street', city: 'Ha Noi', postalCode: '100000', country: 'VN' })),
      'save address', 200, 201).json().id
    made.push({ email, token, address })
  }
  return made
}

// --- checkout -------------------------------------------------------------------------------------------------------

/** Add to cart, ask for a quote, place the order: what a customer's browser does, through the gateway. */
export function checkOut(customer, variant, quantity = 1) {
  must(http.post(`${GATEWAY}/api/cart/items`, ...withJson(customer.token, { productId: variant, quantity }, { name: 'add to cart' })),
    'add to cart', 204)
  must(http.get(`${GATEWAY}/api/orders/quote?addressId=${customer.address}&shippingOption=standard`,
    { ...bearer(customer.token), tags: { name: 'quote' } }), 'quote', 200)
  const placed = must(http.post(`${GATEWAY}/api/orders`,
    ...withJson(customer.token, { addressId: customer.address, shippingOption: 'standard' }, { name: 'place order' })), 'place order', 200)
  return { id: placed.json().orderId, placedAt: Date.now() }
}

/** Follows one order to Paid or Failed, recording how long the saga took. Null if it never settled. */
export function settle(customer, order, timeoutSeconds = 120) {
  for (let i = 0; i < timeoutSeconds * 2; i++) {
    const r = http.get(`${GATEWAY}/api/orders/${order.id}`, { ...bearer(customer.token), tags: { name: 'read order' } })
    const status = r.status === 200 ? r.json().status : ''
    if (status === 'Paid' || status === 'Failed') {
      settleTime.add(Date.now() - order.placedAt)
      return status
    }
    sleep(0.5)
  }
  return null
}

// --- the consistency check (D4) ---------------------------------------------------------------------------------

// The readings, as gauges: k6 keeps them in the summary, which is what the report is built from.
const readingGauges = {}
for (const name of ['placed', 'paid', 'failed', 'stuck', 'starting_stock', 'on_hand', 'reserved']) {
  readingGauges[name] = new Gauge(`check_${name}`)
}

/** Every order these customers placed, as Order reports them: each is one unit of the scenario's one product. */
function statusesOf(people) {
  const statuses = []
  for (const person of people) {
    const page = must(http.get(`${GATEWAY}/api/orders?page=1&pageSize=100`, { ...bearer(person.token), tags: { name: 'check: orders' } }),
      'list orders', 200).json()
    for (const row of page.items) statuses.push(row.status)
  }
  return statuses
}

/**
 * After the run: every order settled, units sold equal units deducted, nothing held - read through the API as a
 * person would, never from a database (Principle I). Each rule broken adds to broken_invariants, whose threshold of
 * zero fails the run.
 */
export function checkConsistency(listed, people, expectedPaid) {
  // Orders settle within seconds of the run's last request; give the stragglers time before calling one stuck.
  let statuses = statusesOf(people)
  for (let i = 0; i < 60 && statuses.some((s) => s === 'Submitted'); i++) { sleep(2); statuses = statusesOf(people) }
  const paid = statuses.filter((s) => s === 'Paid').length
  const failed = statuses.filter((s) => s === 'Failed').length
  const stuck = statuses.length - paid - failed

  // The order settles a moment before Inventory confirms or releases its hold (two messages, no order between them).
  let stock = readStock(listed.product)
  for (let i = 0; i < 30 && stock.quantityReserved !== 0; i++) { sleep(1); stock = readStock(listed.product) }

  const readings = {
    placed: statuses.length, paid, failed, stuck,
    starting_stock: listed.stock, on_hand: stock.quantityOnHand, reserved: stock.quantityReserved,
  }
  for (const [name, value] of Object.entries(readings)) readingGauges[name].add(value)

  const expected = Number(__ENV.EXPECTED_STOCK || listed.stock)
  const rules = [
    ['no order is stuck in Submitted', stuck === 0],
    ['units sold equal units deducted', paid === expected - stock.quantityOnHand],
    ['nothing is held', stock.quantityReserved === 0],
  ]
  if (expectedPaid !== undefined) rules.push([`exactly ${expectedPaid} orders were paid`, paid === expectedPaid])

  for (const [rule, held] of rules) {
    console.log(`${held ? 'ok  ' : 'FAIL'} ${rule}`)
    if (!held) brokenInvariants.add(1)
  }
  console.log(`readings: ${JSON.stringify(readings)}`)
}

/**
 * What the summary must hold for the report (research D7): p99 as well as k6's default percentiles, and each step
 * named - a tagged sub-metric is kept only when a threshold names it, so these always pass and only make it kept.
 */
export function reporting(steps, thresholds) {
  const all = { ...thresholds }
  for (const step of steps) all[`http_req_duration{name:${step}}`] = ['p(99)>=0']
  return { summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'], thresholds: all }
}

/** The summary k6 keeps, with what the scenario adds: the machine, the settings, the invariants. */
export function summary(scenario, settings) {
  return (data) => {
    const file = `/results/${scenario}-${new Date().toISOString().replace(/[:.]/g, '-')}.json`
    const kept = { scenario, machine: __ENV.MACHINE || 'unknown', settings, ...data }
    return {
      [file]: JSON.stringify(kept, null, 2),
      stdout: `\nsummary kept in server/loadtest/results/${file.split('/').pop()}\n`,
    }
  }
}
