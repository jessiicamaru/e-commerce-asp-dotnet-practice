import { AxiosError, AxiosHeaders } from 'axios'
import { beforeEach, describe, expect, it } from 'vitest'
import i18n from '@/config/i18n'
import { refusal } from '@/test/refusal'
import { tooManyAttempts } from './too-many'

const t = i18n.t.bind(i18n)

/** A 429 whose wait is only in the header - the gateway and Identity send both, but a proxy may strip the body. */
function headerOnly(seconds: string) {
  const error = new AxiosError('failed')
  error.response = {
    status: 429,
    data: '',
    statusText: '',
    headers: new AxiosHeaders({ 'retry-after': seconds }),
    config: { headers: new AxiosHeaders() },
  }
  return error
}

describe('tooManyAttempts (specs/062)', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en')
  })

  it('says how many minutes to wait, rounded up', () => {
    expect(tooManyAttempts(t, refusal(429, 'Too many', { retryAfter: 61 }))).toBe(
      'Too many attempts. Try again in 2 minutes.',
    )
  })

  it('never says zero minutes', () => {
    expect(tooManyAttempts(t, refusal(429, 'Too many', { retryAfter: 5 }))).toBe(
      'Too many attempts. Try again in 1 minute.',
    )
  })

  it('reads Retry-After when the body does not say', () => {
    expect(tooManyAttempts(t, headerOnly('300'))).toBe('Too many attempts. Try again in 5 minutes.')
  })

  it('still asks the person to wait when nobody said how long', () => {
    expect(tooManyAttempts(t, refusal(429))).toBe(t('common:error.tooManyAttemptsLater'))
  })

  it('says it in Vietnamese too', async () => {
    await i18n.changeLanguage('vi')
    expect(tooManyAttempts(t, refusal(429, 'Too many', { retryAfter: 240 }))).toContain('4 phút')
  })

  it('has nothing to say about any other refusal', () => {
    expect(tooManyAttempts(t, refusal(401))).toBeNull()
  })
})
