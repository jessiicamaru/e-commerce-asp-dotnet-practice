import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import type { Product } from '@/services/product/types'
import { Questions } from '@/services/question'
import type { Question } from '@/services/question/types'
import { refusal } from '@/test/refusal'
import { renderAsCustomer, renderAsModerator, renderAsSeller, renderSignedOut } from '@/test/render'
import { ProductQuestions } from '.'

const product = (over: Partial<Product> = {}): Product => ({
  id: 'p1', name: 'Fujifilm X-T5', description: null, price: 42_000_000, currency: 'VND', availability: 'InStock', sku: 'FUJI-XT5',
  categoryId: 'c1', isActive: true, imageUrl: null, sellerId: 'seller-9', sellerName: 'Lens House', priceVaries: false,
  variantCount: 1, variants: null, reviewStatus: 'Approved', reviewReason: null, ratingAverage: null, ratingCount: 0, ...over,
})
const question = (over: Partial<Question> = {}): Question => ({
  id: 'q1', productId: 'p1', askerName: 'Mai', body: 'Does it come with the charger?', createdAt: '2026-09-20T08:00:00Z',
  answer: null, answeredAt: null, answerEdited: false, productName: null, hiddenAt: null, hiddenReason: null,
  answerHiddenAt: null, answerHiddenReason: null, ...over,
})
const page = (...items: Question[]) => ({ items, pageNumber: 1, totalPages: 1, totalCount: items.length, hasPreviousPage: false, hasNextPage: false })

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('ProductQuestions (specs/076)', () => {
  it('shows each question with the shop answer, named, or says it is not answered yet', async () => {
    vi.spyOn(Questions, 'forProduct').mockResolvedValue(
      page(question(), question({ id: 'q2', body: 'Weather sealed?', answer: 'Yes, fully.', answeredAt: '2026-09-21T08:00:00Z' })),
    )
    renderSignedOut(<ProductQuestions product={product()} />)

    expect(await screen.findByText('Weather sealed?')).toBeInTheDocument()
    expect(screen.getByText('Yes, fully.')).toBeInTheDocument()
    expect(screen.getByText('Answer from Lens House')).toBeInTheDocument()
    expect(screen.getByText('Not answered yet.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Sign in to ask the seller a question' })).toBeInTheDocument()
  })

  it('lets a customer ask, and sends only the words', async () => {
    vi.spyOn(Questions, 'forProduct').mockResolvedValue(page())
    const ask = vi.spyOn(Questions, 'ask').mockResolvedValue(question())
    const user = userEvent.setup()
    renderAsCustomer(<ProductQuestions product={product()} />)

    const send = await screen.findByRole('button', { name: 'Ask' })
    expect(send).toBeDisabled()
    await user.type(screen.getByLabelText('Ask the seller a question'), '  Charger included?  ')
    await user.click(send)

    await waitFor(() => expect(ask).toHaveBeenCalledWith('p1', 'Charger included?'))
    expect(screen.queryByRole('button', { name: 'Answer' })).not.toBeInTheDocument()
  })

  /** The product's own seller answers, and does not ask their own shop anything. */
  it('gives the product seller an answer box and no ask form', async () => {
    vi.spyOn(Questions, 'forProduct').mockResolvedValue(page(question()))
    const answer = vi.spyOn(Questions, 'answer').mockResolvedValue(question({ answer: 'Yes.' }))
    const user = userEvent.setup()
    renderAsSeller(<ProductQuestions product={product({ sellerId: 'u1' })} />)

    await user.click(await screen.findByRole('button', { name: 'Answer' }))
    await user.type(screen.getByLabelText('Your answer'), 'Yes, in the box.')
    await user.click(screen.getByRole('button', { name: 'Post answer' }))

    await waitFor(() => expect(answer).toHaveBeenCalledWith('q1', 'Yes, in the box.'))
    expect(screen.queryByLabelText('Ask the seller a question')).not.toBeInTheDocument()
  })

  it('gives another seller no answer box - they may ask like anybody', async () => {
    vi.spyOn(Questions, 'forProduct').mockResolvedValue(page(question()))
    renderAsSeller(<ProductQuestions product={product()} />)

    expect(await screen.findByText('Does it come with the charger?')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Answer' })).not.toBeInTheDocument()
    expect(screen.getByLabelText('Ask the seller a question')).toBeInTheDocument()
  })

  /** Staff answer the shop's own products and nobody else's: answering is the seller's to do. */
  it('gives staff an answer box on the shop own product only', async () => {
    vi.spyOn(Questions, 'forProduct').mockResolvedValue(page(question()))
    const { unmount } = renderAsModerator(<ProductQuestions product={product({ sellerId: null, sellerName: null })} />)
    expect(await screen.findByRole('button', { name: 'Answer' })).toBeInTheDocument()
    unmount()

    renderAsModerator(<ProductQuestions product={product()} />)
    expect(await screen.findByText('Does it come with the charger?')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Answer' })).not.toBeInTheDocument()
  })

  it('shows a refusal in the server words - a hidden answer cannot be rewritten', async () => {
    vi.spyOn(Questions, 'forProduct').mockResolvedValue(page(question()))
    vi.spyOn(Questions, 'answer').mockRejectedValue(refusal(409, 'This question or its answer was hidden by a moderator.'))
    const user = userEvent.setup()
    renderAsSeller(<ProductQuestions product={product({ sellerId: 'u1' })} />)

    await user.click(await screen.findByRole('button', { name: 'Answer' }))
    await user.type(screen.getByLabelText('Your answer'), 'Again')
    await user.click(screen.getByRole('button', { name: 'Post answer' }))

    expect(await screen.findByText('This question or its answer was hidden by a moderator.')).toBeInTheDocument()
  })
})
