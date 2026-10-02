import { expect, request, test, type APIRequestContext } from '@playwright/test'
import { Api, type Listed, type Person } from './support/api'
import { findOnPages, signIn } from './support/ui'

/**
 * The storefront in a browser against the real stack (specs/080, #117). Four flows a person takes, in order, each
 * clicking through the real pages: nothing is mocked, so a gateway route missing or a field renamed between a page
 * and the API it calls fails here - which no unit test and no API check can see.
 *
 * <p>
 * The people and products are made through the API first (support/api.ts), fresh for every run, and the products
 * and category are removed at the end.
 * </p>
 */
test.describe.serial('the storefront, end to end', () => {
  let context: APIRequestContext
  let api: Api
  let seller: Person
  let customer: Person
  let moderator: Person
  let camera: Listed
  let waiting: Listed
  let orderId: string

  test.beforeAll(async ({ baseURL }) => {
    context = await request.newContext({ baseURL })
    api = new Api(context)

    const categoryId = await api.category()
    // A long shop name on purpose: it once widened the seller sidebar over every /shop page (specs/117, #238).
    seller = await api.seller('Seller', 'E2e Lens House for Mirrorless and Film Cameras')
    camera = await api.list(seller.token, categoryId, 'camera')
    // A 3:2 photograph, before approval - a new photograph of an approved product sends it back to review (specs/045).
    await api.photograph(seller.token, camera.productId, 600, 400)
    await api.approve(camera.productId)
    await api.stock(seller.token, camera, 5)
    waiting = await api.list(seller.token, categoryId, 'waiting')

    customer = await api.customer('Customer')
    await api.address(customer.token)
    moderator = await api.moderator('Moderator')
  })

  test.afterAll(async () => {
    await api?.cleanUp()
    await context?.dispose()
  })

  test('a moderator approves a product waiting for review', async ({ page }) => {
    await signIn(page, moderator)
    await page.goto('/admin/products')

    const card = await findOnPages(page, page.getByRole('article').filter({ hasText: waiting.name }))
    await card.getByRole('button', { name: 'Approve' }).click()

    await expect(page.getByText(`“${waiting.name}” is on sale.`)).toBeVisible()
    await expect.poll(() => api.productReviewStatus(waiting.productId)).toBe('Approved')
  })

  test('a customer puts a camera in the cart, checks out and it is paid', async ({ page }) => {
    await signIn(page, customer.email)
    await page.goto(`/products/${camera.productId}`)
    await expect(page.getByRole('heading', { name: camera.name })).toBeVisible()
    // Drawn at its own shape, 3:2 - not in a square frame between two bands of white (specs/125, #251).
    const photo = page.getByRole('img', { name: camera.name }).first()
    await expect(photo).toHaveJSProperty('complete', true)
    await expect.poll(async () => {
      const box = (await photo.boundingBox())!
      return Math.round((box.width / box.height) * 100) / 100
    }, { message: 'the photograph keeps its 3:2 shape' }).toBe(1.5)
    await page.getByRole('button', { name: 'Add to cart' }).click()
    await expect(page.getByText('Added 1 to your cart.')).toBeVisible()

    await page.goto('/cart')
    await expect(page.getByText(camera.name)).toBeVisible()
    await page.getByRole('link', { name: 'Check out' }).click()

    // The address saved through the API is chosen already; the first delivery option too.
    await expect(page.getByRole('radio', { name: /E2e Customer/ })).toBeChecked()

    // The line's total is whole and inside the summary card - the narrow column once cut it short (specs/118, #239).
    const lineTotal = page.getByTestId('order-line-total').first()
    await expect(lineTotal).toBeVisible()
    const totalBox = (await lineTotal.boundingBox())!
    const cardBox = (await lineTotal.locator('xpath=ancestor::*[@data-slot="card"][1]').boundingBox())!
    expect(totalBox.x + totalBox.width, 'the line total stays inside its card').toBeLessThanOrEqual(cardBox.x + cardBox.width)
    await page.getByRole('button', { name: /^Place order/ }).click()

    await expect(page).toHaveURL(/\/orders\/[0-9a-f-]{36}$/)
    orderId = page.url().split('/orders/')[1]
    // The saga reserves the stock and the stub gateway approves: the order settles to Paid on its own.
    await expect(page.getByText('Paid. We will start preparing it soon.')).toBeVisible({ timeout: 60_000 })
    // The header stops counting what was just paid for (specs/119, #242).
    await expect(page.getByRole('link', { name: /^Cart \(\d+\)$/ })).toHaveCount(0)
  })

  test('the seller prepares the parcel and ships it', async ({ page }) => {
    await signIn(page, seller.email)
    // The home says the paid sale is waiting for them, and the menu counts it (specs/131, #247).
    await page.goto('/shop')
    await expect(page.getByRole('region', { name: 'Needs you' }).getByRole('link', { name: /1 sale to prepare/ })).toBeVisible()
    await expect(page.getByTestId('seller-sidebar').getByLabel('1 waiting')).toBeVisible()
    await page.goto('/shop/sales')
    // Each sale is named by the order's short reference (specs/132).
    await page.getByRole('link', { name: new RegExp(orderId.slice(0, 8)) }).click()
    await expect(page).toHaveURL(new RegExp(`/shop/sales/${orderId}$`))

    // Nothing in the sidebar reaches into the page, however long the shop's name (specs/117, #238).
    await expect(page.getByTestId('seller-sidebar')).toContainText('E2e Lens House')
    const sidebarRight = await page
      .getByTestId('seller-sidebar')
      .evaluate((aside) => Math.max(...[...aside.children].map((child) => child.getBoundingClientRect().right)))
    const pageLeft = (await page.getByTestId('seller-page').boundingBox())!.x
    expect(sidebarRight, 'the seller sidebar stays left of the page').toBeLessThanOrEqual(pageLeft)

    await page.getByRole('button', { name: 'Start preparing' }).click()
    await expect(page.getByText('Marked as being prepared.')).toBeVisible()

    await page.getByRole('button', { name: 'Mark as shipped' }).click()
    const dialog = page.getByRole('dialog')
    await dialog.getByLabel('Tracking reference').fill('VNPOST-E2E-1')
    await dialog.getByRole('button', { name: 'Mark as shipped' }).click()

    await expect(page.getByText('Marked as shipped.')).toBeVisible()
    await expect(page.getByText('Tracking reference: VNPOST-E2E-1')).toBeVisible()
  })

  test('the customer says it arrived and reviews the camera', async ({ page }) => {
    await signIn(page, customer.email)
    await page.goto(`/orders/${orderId}`)
    await expect(page.getByText('On its way.')).toBeVisible()

    await page.getByRole('button', { name: "I've received it" }).click()
    await page.getByRole('button', { name: 'Yes, it arrived' }).click()
    await expect(page.getByText('Thanks for confirming.')).toBeVisible()

    // The right to review arrives from Order's ParcelDeliveredEvent, through the broker (specs/046): a moment behind.
    await expect(async () => {
      await page.goto(`/products/${camera.productId}`)
      await expect(page.getByText('Review this product')).toBeVisible({ timeout: 3_000 })
    }).toPass({ timeout: 45_000 })

    await page.getByRole('button', { name: '5 stars' }).click()
    await page.getByLabel('What did you think? (optional)').fill('Sharp, quiet and it arrived well packed.')
    await page.getByRole('button', { name: 'Post review' }).click()

    await expect(page.getByText('Thank you - your review is up.')).toBeVisible()
    // In the list of reviews, not only in the form it was typed into.
    await expect(page.getByRole('listitem').filter({ hasText: 'Sharp, quiet and it arrived well packed.' })).toBeVisible()
  })

  /**
   * The catalogue on a phone (specs/122, #245): two products to a row, the first within 1.3 screens, nothing scrolling
   * sideways. Last, so the flows' two approved products are on the shelf - in CI the catalogue is otherwise empty. It
   * was one column, 6,450px for twelve products, the first ~1,350px down (807px now; the old hero alone
   * still put it at 1,355px).
   */
  test('a shopper on a phone sees the catalogue two to a row', async ({ browser, baseURL }) => {
    const context = await browser.newContext({ baseURL, viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true })
    const page = await context.newPage()
    await page.addInitScript(() => localStorage.setItem('language', 'en'))
    await page.goto('/')

    const cards = page.getByTestId('catalogue-grid').getByRole('listitem')
    await expect(cards.nth(1)).toBeVisible()
    const [first, second] = [(await cards.nth(0).boundingBox())!, (await cards.nth(1).boundingBox())!]
    expect(second.y, 'the second product shares the first row').toBeCloseTo(first.y, 0)
    const top = await cards.nth(0).evaluate((card) => card.getBoundingClientRect().top + window.scrollY)
    expect(top, 'the first product starts within 1.3 screens').toBeLessThan(1100)
    expect(await page.evaluate(() => document.documentElement.scrollWidth), 'nothing scrolls sideways').toBeLessThanOrEqual(390)
    await context.close()
  })
})
