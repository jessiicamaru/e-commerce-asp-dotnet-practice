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
    seller = await api.seller('Seller', 'E2e Lens House')
    camera = await api.list(seller.token, categoryId, 'camera')
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
    await signIn(page, moderator.email)
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
    await page.getByRole('button', { name: 'Add to cart' }).click()
    await expect(page.getByText('Added 1 to your cart.')).toBeVisible()

    await page.goto('/cart')
    await expect(page.getByText(camera.name)).toBeVisible()
    await page.getByRole('link', { name: 'Check out' }).click()

    // The address saved through the API is chosen already; the first delivery option too.
    await expect(page.getByRole('radio', { name: /E2e Customer/ })).toBeChecked()
    await page.getByRole('button', { name: /^Place order/ }).click()

    await expect(page).toHaveURL(/\/orders\/[0-9a-f-]{36}$/)
    orderId = page.url().split('/orders/')[1]
    // The saga reserves the stock and the stub gateway approves: the order settles to Paid on its own.
    await expect(page.getByText('Paid. We will start preparing it soon.')).toBeVisible({ timeout: 60_000 })
  })

  test('the seller prepares the parcel and ships it', async ({ page }) => {
    await signIn(page, seller.email)
    await page.goto('/shop/sales')
    await page.getByRole('link', { name: /^Placed / }).first().click()
    await expect(page).toHaveURL(new RegExp(`/shop/sales/${orderId}$`))

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
})
