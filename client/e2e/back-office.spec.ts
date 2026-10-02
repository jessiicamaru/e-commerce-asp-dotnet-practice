import { expect, request, test, type APIRequestContext } from '@playwright/test'
import { Api, type Person } from './support/api'
import { BACK_OFFICE, signInToBackOffice } from './support/ui'

/**
 * The back office in a browser (specs/136, 137, ADR-003): staff sign in on an origin of their own, with the code, and
 * the session is the back office's own.
 */
test.describe.serial('the back office', () => {
  let context: APIRequestContext
  let api: Api
  let moderator: Person
  let customer: Person

  test.beforeAll(async ({ baseURL }) => {
    context = await request.newContext({ baseURL })
    api = new Api(context)
    moderator = await api.moderator('Office')
    customer = await api.customer('Shopper')
  })

  test.afterAll(async () => {
    await api?.cleanUp()
    await context?.dispose()
  })

  test('a moderator signs in with a code, lands on the console, stays signed in on a reload, and signs out', async ({ page }) => {
    await signInToBackOffice(page, moderator)

    // The console's home sends a moderator to their own page (specs/043, 137).
    await expect(page).toHaveURL(new RegExp(`^${BACK_OFFICE}/moderation$`))
    await expect(page.getByText(moderator.email)).toBeVisible()

    // The back office's own refresh cookie brings the session back.
    await page.reload()
    await expect(page).toHaveURL(/\/moderation$/)
    await expect(page.getByText(moderator.email)).toBeVisible()

    await page.getByRole('button', { name: 'Sign out' }).click()
    await expect(page).toHaveURL(/\/sign-in$/)
    await page.reload()
    await expect(page).toHaveURL(/\/sign-in$/)
  })

  test('a customer is told the back office is for staff', async ({ page }) => {
    await signInToBackOffice(page, customer)

    await expect(page.getByText('This is for staff')).toBeVisible()
    await expect(page.getByRole('navigation')).toHaveCount(0)
  })
})
