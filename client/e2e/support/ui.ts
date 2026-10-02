import { expect, type Locator, type Page } from '@playwright/test'
import { PASSWORD, type Person } from './api'
import { freshCode } from './totp'

/**
 * Signs in through the sign-in page, as a person does - in English, whatever the machine's own language, so the
 * words the flows look for are the same on every machine and in CI.
 */
export async function signIn(page: Page, who: string | Person): Promise<void> {
  const person = typeof who === 'string' ? undefined : who
  await page.addInitScript(() => localStorage.setItem('language', 'en'))
  await page.goto('/sign-in')
  await page.getByLabel('Email').fill(person?.email ?? (who as string))
  await page.getByLabel('Password', { exact: true }).fill(PASSWORD)
  await page.getByRole('button', { name: 'Sign in' }).click()

  // Staff sign in with a code too (specs/110): typed from the authenticator secret the run enrolled them with.
  if (person?.totpSecret) {
    const { code, step } = await freshCode(person.totpSecret, person.lastStep)
    await page.getByLabel('Code').fill(code)
    await page.getByRole('button', { name: 'Verify' }).click()
    person.lastStep = step
  }

  await expect(page).not.toHaveURL(/\/sign-in/)
}

/**
 * Finds something in a paged list (PAGE_SIZE 12), turning pages until it shows: a queue a machine shares with older
 * runs, or with a person, is not guaranteed to hold this run's item on its first page.
 *
 * <p>
 * Each page is given a few seconds to show it rather than checked at once: after "Next" the counter ("Showing
 * 13-14") changes at the click, while the list keeps the previous page's rows until the new page arrives.
 * </p>
 */
export async function findOnPages(page: Page, item: Locator): Promise<Locator> {
  const seen: string[] = []
  for (let turned = 0; turned < 50; turned++) {
    try {
      await expect(item.first()).toBeVisible({ timeout: 5_000 })
      return item.first()
    } catch {
      // not on this page
    }
    seen.push((await page.getByText(/^Showing /).first().textContent().catch(() => null)) ?? '?')
    const next = page.getByLabel('Next')
    if ((await next.count()) === 0 || (await next.first().getAttribute('aria-disabled')) === 'true') break
    await next.first().click()
  }
  throw new Error(`Not found on any page of the list (looked at: ${seen.join('; ')}).`)
}

/**
 * The back office (specs/136, 137), against compose at portal.localhost - a different HOST from the storefront's
 * localhost, because cookies ignore the port.
 */
export const BACK_OFFICE = process.env.E2E_BACK_OFFICE_URL ?? 'http://portal.localhost:8089'

/** Signs in to the back office as a person does: the password, then - for staff - the code. */
export async function signInToBackOffice(page: Page, person: Person): Promise<void> {
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

  // Signed in only once the page has left sign-in: the code is exchanged after the click, and a navigation before it
  // answers would abandon the exchange and land back here - which CI, slower than a laptop, did (specs/137).
  await expect(page).not.toHaveURL(/\/sign-in/)
}
