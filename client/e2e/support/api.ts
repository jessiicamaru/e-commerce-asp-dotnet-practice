import { expect, type APIRequestContext } from '@playwright/test'
import { deflateSync } from 'node:zlib'
import { freshCode } from './totp'

/**
 * The data the browser flows start from, made through the same API a person's clicks reach (specs/080) - never by
 * writing SQL - so setting a flow up exercises the gateway routes too. Every run makes its own people and products,
 * and `cleanUp` removes the products and the category it made.
 */

export const PASSWORD = 'E2e-Passw0rd!1'

const MAILPIT = process.env.E2E_MAILPIT_URL ?? 'http://localhost:8025'

export interface Person {
  id: string
  email: string
  token: string
  /** Staff only (specs/110): the authenticator secret this run enrolled them with, and the last window a code used. */
  totpSecret?: string
  lastStep?: number
}

export interface Listed {
  productId: string
  variantId: string
  name: string
}

/** Signed in once per run - one worker, serial flows - and reused (specs/110). */
let adminToken: string | undefined

function required(name: string): string {
  const value = process.env[name]
  if (!value) throw new Error(`${name} is not set: the end-to-end flows sign in as the seeded administrator.`)
  return value
}

export class Api {
  private readonly made: { products: string[]; categories: string[] } = { products: [], categories: [] }
  private readonly run = `${Date.now()}`

  constructor(private readonly request: APIRequestContext) {}

  async login(email: string, password = PASSWORD): Promise<string> {
    const response = await this.request.post('/api/auth/login', { data: { email, password } })
    expect(response.status(), `sign in as ${email}`).toBe(200)
    return (await response.json()).token
  }

  /**
   * The administrator's token, signed in once per run with a code from ADMIN_TOTP_SECRET (specs/110) and reused: a
   * code works once, and an access token outlives the whole run.
   */
  async admin(): Promise<string> {
    adminToken ??= await this.signInWithCode(required('ADMIN_EMAIL'), required('ADMIN_PASSWORD'), required('ADMIN_TOTP_SECRET'))
    return adminToken
  }

  /** Both steps of signing in (specs/110): the password for a challenge, then the code for the session. */
  async signInWithCode(email: string, password: string, secret: string, person?: Person): Promise<string> {
    const first = await this.request.post('/api/auth/login', { data: { email, password } })
    expect(first.status(), `sign in as ${email}`).toBe(200)
    const { challenge } = await first.json()
    expect(challenge, `${email} is asked for a code`).toBeTruthy()

    const { code, step } = await freshCode(secret, person?.lastStep)
    // Staff act from the back office (specs/138): only a session made there carries a staff role - Identity knows the app
    // by the Origin of the request that exchanges the code.
    const second = await this.request.post('/api/auth/login/two-factor', {
      data: { challenge, code },
      headers: { Origin: process.env.E2E_BACK_OFFICE_URL ?? 'http://portal.localhost:8089' },
    })
    expect(second.status(), `the code for ${email}`).toBe(200)
    if (person) person.lastStep = step
    return (await second.json()).token
  }

  async customer(firstName: string): Promise<Person> {
    const email = `e2e-${firstName.toLowerCase()}-${this.run}@local.test`
    const response = await this.request.post('/api/auth/register', {
      data: { email, password: PASSWORD, firstName, lastName: 'E2e' },
    })
    expect(response.status(), `register ${email}`).toBe(200)
    const body = await response.json()
    return { id: body.id, email, token: body.token }
  }

  /** A seller whose shop an administrator approved: registered, address confirmed from Mailpit, approved, signed in again. */
  async seller(firstName: string, shopName: string): Promise<Person> {
    const email = `e2e-${firstName.toLowerCase()}-${this.run}@local.test`
    const registered = await this.request.post('/api/auth/register-seller', {
      data: { email, password: PASSWORD, firstName, lastName: 'E2e', shopName: `${shopName} ${this.run}` },
    })
    expect(registered.status(), `register seller ${email}`).toBe(200)
    const first = await registered.json()

    await this.confirmEmail(email)
    const mine = await this.request.get('/api/shop-applications/mine', { headers: this.bearer(first.token) })
    const [application] = await mine.json()
    const admin = await this.admin()
    const approved = await this.request.post(`/api/shop-applications/${application.id}/approve`, { headers: this.bearer(admin) })
    expect(approved.status(), 'approve the shop').toBe(200)

    // The Seller role reaches a session at its next sign-in (specs/044).
    return { id: first.id, email, token: await this.login(email) }
  }

  /**
   * A moderator, enrolled in two-factor sign-in as staff must be (specs/110): granted the role, set up with an
   * authenticator secret this run keeps, confirmed with a code, then signed in with the next one.
   */
  async moderator(firstName: string): Promise<Person> {
    const person: Person = await this.customer(firstName)
    const granted = await this.request.put(`/api/users/${person.id}/roles/Moderator`, { headers: this.bearer(await this.admin()) })
    expect(granted.ok(), 'grant Moderator').toBe(true)

    // Signed in without a second factor, a moderator is told to set it up - and holds no Moderator yet.
    const unverified = await this.request.post('/api/auth/login', { data: { email: person.email, password: PASSWORD } })
    const session = await unverified.json()
    expect(session.twoFactor, 'a new moderator must set up two-factor sign-in').toBe('SetupRequired')

    const setup = await this.request.post('/api/auth/me/two-factor/setup', { headers: this.bearer(session.token) })
    expect(setup.status(), 'start two-factor setup').toBe(200)
    person.totpSecret = (await setup.json()).secret as string
    const { code, step } = await freshCode(person.totpSecret, undefined)
    const confirmed = await this.request.post('/api/auth/me/two-factor/confirm', { data: { code }, headers: this.bearer(session.token) })
    expect(confirmed.status(), 'confirm two-factor setup').toBe(200)
    person.lastStep = step

    person.token = await this.signInWithCode(person.email, PASSWORD, person.totpSecret, person)
    return person
  }

  /** The link in the confirmation email, read from Mailpit - a real address is confirmed the same way. */
  async confirmEmail(email: string): Promise<void> {
    let token: string | null = null
    for (let attempt = 0; attempt < 40 && !token; attempt++) {
      const found = await this.request.get(`${MAILPIT}/api/v1/search?query=${encodeURIComponent(`to:${email}`)}`)
      for (const message of (await found.json()).messages ?? []) {
        const full = await (await this.request.get(`${MAILPIT}/api/v1/message/${message.ID}`)).json()
        const link = /confirm-email\?token=([^\s)]+)/.exec(full.Text ?? '')
        if (link) token = decodeURIComponent(link[1])
      }
      if (!token) await new Promise((resolve) => setTimeout(resolve, 1000))
    }
    expect(token, `a confirmation email for ${email} in Mailpit`).not.toBeNull()
    const confirmed = await this.request.post('/api/auth/confirm-email', { data: { token } })
    expect(confirmed.status(), 'confirm the address').toBe(204)
  }

  async category(): Promise<string> {
    const response = await this.request.post('/api/categories', {
      data: { name: `E2e ${this.run}`, slug: `e2e-${this.run}`, description: null, parentCategoryId: null },
      headers: this.bearer(await this.admin()),
    })
    expect([200, 201], 'create a category').toContain(response.status())
    const id = (await response.json()).id
    this.made.categories.push(id)
    return id
  }

  /** A product a seller lists - waiting for review until `approve`. Its first variant reuses its id (specs/020). */
  async list(sellerToken: string, categoryId: string, label: string): Promise<Listed> {
    const sku = `E2E${label.toUpperCase()}${this.run}`.slice(0, 30)
    const name = `E2e ${label} ${this.run}`
    const response = await this.request.post('/api/products', {
      data: { name, description: 'Listed by the browser tests.', price: 1_250_000, sku, categoryId },
      headers: this.bearer(sellerToken),
    })
    expect(response.status(), `list ${name}`).toBe(200)
    const product = await response.json()
    this.made.products.push(product.id)
    return { productId: product.id, variantId: product.variants?.[0]?.id ?? product.id, name }
  }

  /**
   * A photograph for a listing, uploaded the way the seller's page does - a plain PNG of the given size, made here so
   * the run needs no image file (specs/125: the product page must draw it at its own shape).
   */
  async photograph(sellerToken: string, productId: string, width: number, height: number): Promise<void> {
    const response = await this.request.put(`/api/products/${productId}/image`, {
      multipart: { file: { name: 'photo.png', mimeType: 'image/png', buffer: png(width, height) } },
      headers: this.bearer(sellerToken),
    })
    expect(response.status(), 'upload the photograph').toBe(200)
  }

  async approve(productId: string): Promise<void> {
    const response = await this.request.post(`/api/products/${productId}/approve`, { headers: this.bearer(await this.admin()) })
    expect(response.status(), 'approve the product').toBe(200)
  }

  /** Stock a seller sets, then the wait for Catalog's read model to say so - it hears it from Inventory's event. */
  async stock(sellerToken: string, listed: Listed, quantity: number): Promise<void> {
    // The stock row arrives from Catalog's ProductCreated event; until then the PUT is the "not arrived yet" 404.
    await expect
      .poll(async () => (await this.request.put(`/api/stock/${listed.variantId}`, { data: { quantityOnHand: quantity }, headers: this.bearer(sellerToken) })).status(), {
        message: 'stock the variant',
        timeout: 30_000,
      })
      .toBe(200)
    await expect
      .poll(async () => (await (await this.request.get(`/api/products/${listed.productId}`)).json()).availability, {
        message: 'the catalogue says it is in stock',
        timeout: 30_000,
      })
      .toBe('InStock')
  }

  async address(customerToken: string): Promise<string> {
    const response = await this.request.post('/api/addresses', {
      data: {
        recipientName: 'E2e Customer',
        line1: '12 Ly Thuong Kiet',
        line2: null,
        city: 'Ha Noi',
        region: null,
        postalCode: '100000',
        country: 'VN',
        phone: '+84 912 345 678',
      },
      headers: this.bearer(customerToken),
    })
    expect(response.status(), 'save an address').toBe(201)
    return (await response.json()).id
  }

  async productReviewStatus(productId: string): Promise<string> {
    const response = await this.request.get(`/api/products/${productId}`, { headers: this.bearer(await this.admin()) })
    return (await response.json()).reviewStatus
  }

  /** Removes what this run listed, then its category - what Bruno's teardown folder does (specs/073). */
  async cleanUp(): Promise<void> {
    const admin = await this.admin()
    for (const id of this.made.products) await this.request.delete(`/api/products/${id}`, { headers: this.bearer(admin) })
    for (const id of this.made.categories) await this.request.delete(`/api/categories/${id}`, { headers: this.bearer(admin) })
  }

  private bearer(token: string) {
    return { Authorization: `Bearer ${token}` }
  }
}

/** A solid PNG of this size - signature, IHDR, one IDAT of unfiltered rows, IEND. */
function png(width: number, height: number): Buffer {
  const crcTable = Array.from({ length: 256 }, (_, n) => {
    let c = n
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1
    return c >>> 0
  })
  const crc = (bytes: Buffer) => {
    let c = 0xffffffff
    for (const byte of bytes) c = crcTable[(c ^ byte) & 0xff] ^ (c >>> 8)
    return (c ^ 0xffffffff) >>> 0
  }
  const chunk = (type: string, data: Buffer) => {
    const length = Buffer.alloc(4)
    length.writeUInt32BE(data.length)
    const body = Buffer.concat([Buffer.from(type, 'ascii'), data])
    const sum = Buffer.alloc(4)
    sum.writeUInt32BE(crc(body))
    return Buffer.concat([length, body, sum])
  }
  const header = Buffer.alloc(13)
  header.writeUInt32BE(width, 0)
  header.writeUInt32BE(height, 4)
  header.set([8, 2, 0, 0, 0], 8) // 8-bit RGB
  const row = Buffer.concat([Buffer.from([0]), Buffer.alloc(width * 3, 0x55)])
  const pixels = deflateSync(Buffer.concat(Array.from({ length: height }, () => row)))
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', header),
    chunk('IDAT', pixels),
    chunk('IEND', Buffer.alloc(0)),
  ])
}
