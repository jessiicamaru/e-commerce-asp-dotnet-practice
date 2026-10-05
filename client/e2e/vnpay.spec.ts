import { request, type APIRequestContext } from '@playwright/test'
import { Api, type Listed, type Person } from './support/api'
import { signIn } from './support/ui'
import { expect, test } from './support/test'

/**
 * Paying at VNPay, in a browser (specs/143): the customer leaves the shop for the gateway's page - the simulator in
 * development - pays or cancels there, and comes back to an order that the GATEWAY'S SIGNED CALL settled, not the
 * address they came back on.
 *
 * <p>
 * Runs only against a stack whose Payment takes payments through VNPay (`PAYMENT_PROVIDER=VnPay`, which compose pairs
 * with the simulator on :5064); against the stub it says so and skips, because the stub settles every order on its
 * own and there is no gateway to go to.
 * </p>
 */
test.describe.serial('paying at VNPay, end to end', () => {
  let context: APIRequestContext
  let api: Api
  let customer: Person
  let camera: Listed

  test.beforeAll(async ({ baseURL }) => {
    context = await request.newContext({ baseURL, ignoreHTTPSErrors: process.env.E2E_IGNORE_HTTPS_ERRORS === '1' })
    const health = await (await context.get('/api/payment/health')).json()
    test.skip(health.configuredOutcome !== 'Customer', `Payment takes payments through ${health.provider}, not VNPay`)

    api = new Api(context)
    const categoryId = await api.category()
    const seller = await api.seller('Vnpayseller', 'E2e VNPay Camera Shop')
    camera = await api.list(seller.token, categoryId, 'vnpay camera')
    await api.approve(camera.productId)
    await api.stock(seller.token, camera, 5)
    customer = await api.customer('Vnpaybuyer')
    await api.address(customer.token)
  })

  test.afterAll(async () => {
    await api?.cleanUp()
    await context?.dispose()
  })

  async function placeOrder(page: import('@playwright/test').Page) {
    await signIn(page, customer.email)
    await page.goto(`/products/${camera.productId}`)
    await page.getByRole('button', { name: 'Add to cart' }).click()
    await expect(page.getByText('Added 1 to your cart.')).toBeVisible()
    await page.goto('/checkout')
    // The payment card says where the customer pays, and that the sandbox moves no money.
    await expect(page.getByText(/pay on VNPay's page/)).toBeVisible()
    await page.getByRole('button', { name: /^Place order/ }).click()
    await expect(page).toHaveURL(/\/orders\/[0-9a-f-]{36}$/)
  }

  test('a customer pays at VNPay and the order is paid', async ({ page }) => {
    await placeOrder(page)
    await expect(page.getByText('Your items are reserved. Waiting for your payment.')).toBeVisible({ timeout: 60_000 })

    await page.getByRole('link', { name: /Pay with VNPay/ }).click()
    // The gateway's page - the simulator accepts only a link Payment signed correctly.
    await expect(page.getByRole('heading', { name: /VNPay/ })).toBeVisible()
    await expect(page.locator('#amount')).toContainText('VND')
    await page.getByRole('button', { name: 'Pay' }).click()

    // Back at the shop, on the order the reference names - settled by the IPN.
    await expect(page).toHaveURL(/\/orders\/[0-9a-f-]{36}$/)
    await expect(page.getByText('Paid. We will start preparing it soon.')).toBeVisible({ timeout: 60_000 })
  })

  test('a customer who cancels at VNPay is not charged, and the order fails', async ({ page }) => {
    await placeOrder(page)
    await page.getByRole('link', { name: /Pay with VNPay/ }).click({ timeout: 60_000 })
    await page.getByRole('button', { name: 'Cancel' }).click()

    await expect(page).toHaveURL(/\/orders\/[0-9a-f-]{36}$/)
    await expect(page.getByText(/Not placed/)).toBeVisible({ timeout: 60_000 })
  })

  test('a return address claiming success for somebody else\'s order shows nothing of it', async ({ page }) => {
    await signIn(page, customer.email)
    // A made-up reference with a "paid" code: the shop reads the order it names, and it is not this customer's.
    await page.goto('/payment/vnpay-return?vnp_TxnRef=0199a1b2c3d4e5f60718293a4b5c6d7e&vnp_ResponseCode=00')
    await expect(page.getByText('Order not found.')).toBeVisible()
  })
})
