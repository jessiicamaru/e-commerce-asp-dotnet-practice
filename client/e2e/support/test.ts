import { test as base, expect, type BrowserContext, type Page } from '@playwright/test'

/**
 * Collects every refusal the security headers cause in a context (specs/151, #294). Chromium logs a Content-Security-
 * Policy refusal - a script, a style, an image, a connection the policy does not allow - as a console error naming the
 * policy, and fails a subresource that Cross-Origin-Embedder-Policy or -Resource-Policy refuses with
 * ERR_BLOCKED_BY_RESPONSE. Nothing else is collected. Pages opened later in the context are watched too.
 */
export function watchContentSecurityPolicy(context: BrowserContext): string[] {
  const violations: string[] = []
  const watch = (page: Page) => {
    page.on('console', (message) => {
      if (/Content Security Policy|Cross-Origin-(Embedder|Resource)-Policy/i.test(message.text())) violations.push(`${page.url()}: ${message.text()}`)
    })
    page.on('requestfailed', (request) => {
      const failure = request.failure()?.errorText ?? ''
      if (/BLOCKED_BY_RESPONSE|BLOCKED_BY_CSP/i.test(failure)) violations.push(`${page.url()}: ${request.url()} ${failure}`)
    })
  }
  context.pages().forEach(watch)
  context.on('page', watch)
  return violations
}

/**
 * Playwright's `test`, with one more check after every test: the browser reported no Content-Security-Policy
 * violation. The headers are only worth having if the shop works under them, and a refusal is silent on the page - an
 * icon missing, a toast unstyled - so a flow could pass while the policy broke it.
 */
export const test = base.extend<{ contentSecurityPolicy: string[] }>({
  contentSecurityPolicy: [
    async ({ context }, use) => {
      const violations = watchContentSecurityPolicy(context)
      await use(violations)
      expect(violations, 'Content-Security-Policy violations').toEqual([])
    },
    { auto: true },
  ],
})

export { expect }
