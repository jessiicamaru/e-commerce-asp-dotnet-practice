import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Auth } from '@/services/auth'
import { refusal } from '@/test/refusal'
import { ForgotPasswordPage } from '.'

async function ask(email: string) {
  render(
    <MemoryRouter>
      <ForgotPasswordPage />
    </MemoryRouter>,
  )
  const user = userEvent.setup()
  await user.type(screen.getByLabelText(i18n.t('auth:forgot.email')), email)
  await user.click(screen.getByRole('button', { name: i18n.t('auth:forgot.submit') }))
}

describe('ForgotPasswordPage (specs/061)', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en')
  })
  afterEach(() => vi.restoreAllMocks())

  it('asks for a link for the address typed', async () => {
    const forgot = vi.spyOn(Auth, 'forgotPassword').mockResolvedValue()

    await ask('lan@demo.test')

    expect(forgot).toHaveBeenCalledWith('lan@demo.test')
  })

  /** #28: the page says the same thing for any address - it cannot know, and must not guess. */
  it('says the same thing whether or not the address has an account', async () => {
    vi.spyOn(Auth, 'forgotPassword').mockResolvedValue()

    await ask('nobody@demo.test')

    expect(await screen.findByRole('status')).toHaveTextContent(
      i18n.t('auth:forgot.sent', { email: 'nobody@demo.test' }),
    )
    expect(screen.queryByRole('button', { name: i18n.t('auth:forgot.submit') })).toBeNull()
  })

  it('says so when the request itself failed, rather than claiming a link was sent', async () => {
    vi.spyOn(Auth, 'forgotPassword').mockRejectedValue(refusal(503))

    await ask('lan@demo.test')

    expect(await screen.findByRole('alert')).toHaveTextContent(i18n.t('auth:forgot.failed'))
    expect(screen.queryByRole('status')).toBeNull()
  })

  it('speaks Vietnamese too', async () => {
    await i18n.changeLanguage('vi')
    vi.spyOn(Auth, 'forgotPassword').mockResolvedValue()

    await ask('lan@demo.test')

    expect(await screen.findByRole('status')).toHaveTextContent('lan@demo.test')
    expect(i18n.t('auth:forgot.sent', { email: 'x' })).not.toBe(i18n.t('auth:forgot.sent', { email: 'x', lng: 'en' }))
  })
})
