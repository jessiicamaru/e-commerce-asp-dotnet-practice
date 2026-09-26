import { expect, type Locator, type Page } from '@playwright/test'
import { PASSWORD } from './api'

/**
 * Signs in through the sign-in page, as a person does - in English, whatever the machine's own language, so the
 * words the flows look for are the same on every machine and in CI.
 */
export async function signIn(page: Page, email: string): Promise<void> {
  await page.addInitScript(() => localStorage.setItem('language', 'en'))
  await page.goto('/sign-in')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password', { exact: true }).fill(PASSWORD)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page).not.toHaveURL(/\/sign-in/)
}

/**
 * Finds something in a paged list (PAGE_SIZE 12), turning pages until it shows: a queue a machine shares with older
 * runs, or with a person, is not guaranteed to hold this run's item on its first page.
 */
export async function findOnPages(page: Page, item: Locator): Promise<Locator> {
  const seen: string[] = []
  for (let turned = 0; turned < 50; turned++) {
    await page.getByText(/^Showing |^Nothing here\.$/).first().waitFor()
    if ((await item.count()) > 0) return item.first()
    const next = page.getByLabel('Next')
    if ((await next.count()) === 0 || !(await next.first().isEnabled()) || (await next.first().getAttribute('aria-disabled')) === 'true') break
    const before = await page.getByText(/^Showing /).first().textContent()
    seen.push(before ?? '?')
    await next.first().click()
    await expect(page.getByText(/^Showing /).first()).not.toHaveText(before ?? '')
  }
  throw new Error(`Not found on any page of the list (turned through: ${seen.join('; ') || 'one page'}).`)
}
