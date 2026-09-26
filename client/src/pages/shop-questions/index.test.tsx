import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Questions } from '@/services/question'
import type { Question } from '@/services/question/types'
import { renderAsSeller } from '@/test/render'
import { ShopQuestionsPage } from '.'

const question = (over: Partial<Question> = {}): Question => ({
  id: 'q1', productId: 'p1', askerName: 'Mai', body: 'Charger?', createdAt: '2026-09-20T08:00:00Z',
  answer: null, answeredAt: null, answerEdited: false, productName: 'Fujifilm X-T5',
  hiddenAt: null, hiddenReason: null, answerHiddenAt: null, answerHiddenReason: null, ...over,
})
const page = (...items: Question[]) => ({ items, pageNumber: 1, totalPages: 1, totalCount: items.length, hasPreviousPage: false, hasNextPage: false })

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('ShopQuestionsPage (specs/076)', () => {
  it('lists what waits for the seller, linked to the product, then what they answered', async () => {
    const toAnswer = vi
      .spyOn(Questions, 'toAnswer')
      .mockImplementation(async (answered) =>
        answered ? page(question({ answer: 'Yes.', answeredAt: '2026-09-21T08:00:00Z' })) : page(question()),
      )
    const user = userEvent.setup()
    renderAsSeller(<ShopQuestionsPage />, '/shop/questions')

    expect(await screen.findByRole('link', { name: 'On Fujifilm X-T5' })).toHaveAttribute('href', '/products/p1')
    expect(screen.getByRole('button', { name: 'Answer' })).toBeInTheDocument()

    await user.click(screen.getByRole('tab', { name: 'Answered' }))

    expect(await screen.findByRole('button', { name: 'Edit answer' })).toBeInTheDocument()
    expect(screen.getByText('Your answer')).toBeInTheDocument()
    expect(toAnswer.mock.calls.map((c) => c[0])).toEqual([false, true])
  })

  it('says when nothing is waiting', async () => {
    vi.spyOn(Questions, 'toAnswer').mockResolvedValue(page())
    renderAsSeller(<ShopQuestionsPage />, '/shop/questions')

    expect(await screen.findByText('No questions are waiting for an answer.')).toBeInTheDocument()
  })
})
