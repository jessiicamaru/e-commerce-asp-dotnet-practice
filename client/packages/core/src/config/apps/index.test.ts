import { afterEach, describe, expect, it } from 'vitest'
import { backOfficeUrl, consoleAddress, storefrontUrl } from '.'

afterEach(() => {
  delete window.__APP_CONFIG__
})

describe('where the other app is (specs/137)', () => {
  it('uses the development addresses when nothing is configured', () => {
    expect(storefrontUrl('/products/p1')).toBe('http://localhost:5173/products/p1')
    expect(backOfficeUrl()).toBe('http://portal.localhost:5174/')
  })

  it('uses what nginx wrote, without a doubled slash', () => {
    window.__APP_CONFIG__ = { storefrontUrl: 'https://ecommerce.example/', backOfficeUrl: 'https://portal.ecommerce.example' }

    expect(storefrontUrl('products/p1')).toBe('https://ecommerce.example/products/p1')
    expect(backOfficeUrl('/users')).toBe('https://portal.ecommerce.example/users')
  })

  it('sends a console address from before the move to the same page in the back office', () => {
    expect(consoleAddress('/admin')).toBe('http://portal.localhost:5174/')
    expect(consoleAddress('/admin/email-delivery')).toBe('http://portal.localhost:5174/email-delivery')
    expect(consoleAddress('/admin/users?role=Moderator')).toBe('http://portal.localhost:5174/users?role=Moderator')
    expect(consoleAddress('/admin?status=Failed')).toBe('http://portal.localhost:5174/?status=Failed')
  })

  it('leaves every other address alone', () => {
    expect(consoleAddress('/orders/o1')).toBeNull()
    expect(consoleAddress('/administrators')).toBeNull()
  })
})
