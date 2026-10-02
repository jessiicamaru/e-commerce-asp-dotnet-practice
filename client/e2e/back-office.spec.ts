import { expect, request, test, type APIRequestContext, type Page } from '@playwright/test'
import { Api, PASSWORD, type Person } from './support/api'
import { freshCode } from './support/totp'

/**
 * The back office in a browser (specs/136, ADR-003): staff sign in on an origin of their own, with the code, and the
 * session is the back office's own. Against compose, at portal.localhost - a different HOST from the storefront's
 * localhost, because cookies ignore the port.
 */
const BACK_OFFICE = process.env.E2E_BACK_OFFICE_URL ?? 'http://portal.localhost:8089'

async function signInToBackOffice(page: Page, person: Person) {
  await page.addInitScript(() => localStorage.setItem('language', 'en'))
  await page.goto(BACK_OFFICE)
  await expect(page).toHaveURL(/\/sign-in$/)
  await page.getByLabel('Email').fill(person.email)
  await page.getByLabel('Password', { exact: true }).fill(PASSWORD)
  await page.getByRole('button', { name: 'Sign in' }).click()
  if (person.totpSecret) {
    const { code, step } = await freshCode(person.totpSecret, person.lastStep)
    await page.getByLabel('Code').fill(code)
    await page.getByRole('button', { name: 'Verify' }).click()
    person.lastStep = step
  }
}

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

  test('a moderator signs in with a code, stays signed in on a reload, and signs out', async ({ page }) => {
    await signInToBackOffice(page, moderator)

    await expect(page.getByRole('heading', { name: /^Signed in as/ })).toBeVisible()
    await expect(page.getByText('You are a moderator.')).toBeVisible()

    // The back office's own refresh cookie brings the session back.
    await page.reload()
    await expect(page.getByText('You are a moderator.')).toBeVisible()

    await page.getByRole('button', { name: 'Sign out' }).click()
    await expect(page).toHaveURL(/\/sign-in$/)
    await page.reload()
    await expect(page).toHaveURL(/\/sign-in$/)
  })

  test('a customer is told the back office is for staff', async ({ page }) => {
    await signInToBackOffice(page, customer)

    await expect(page.getByText('This is for staff')).toBeVisible()
    await expect(page.getByRole('heading', { name: /^Signed in as/ })).toHaveCount(0)
  })
})
