import { expect, type APIRequestContext } from '@playwright/test'

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
}

export interface Listed {
  productId: string
  variantId: string
  name: string
}

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

  async admin(): Promise<string> {
    return this.login(required('ADMIN_EMAIL'), required('ADMIN_PASSWORD'))
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

  async moderator(firstName: string): Promise<Person> {
    const person = await this.customer(firstName)
    const granted = await this.request.put(`/api/users/${person.id}/roles/Moderator`, { headers: this.bearer(await this.admin()) })
    expect(granted.ok(), 'grant Moderator').toBe(true)
    return { ...person, token: await this.login(person.email) }
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
