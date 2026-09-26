import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Questions } from '@/services/question'
import type { Question } from '@/services/question/types'
import { renderAsModerator } from '@/test/render'
import { AdminQuestionsPage } from '.'

const question = (over: Partial<Question> = {}): Question => ({
  id: 'q1', productId: 'p1', askerName: 'Mai', body: 'Charger?', createdAt: '2026-09-20T08:00:00Z',
  answer: 'Buy it elsewhere.', answeredAt: '2026-09-21T08:00:00Z', answerEdited: false, productName: 'Fujifilm X-T5',
  hiddenAt: null, hiddenReason: null, answerHiddenAt: null, answerHiddenReason: null, ...over,
})
const page = (...items: Question[]) => ({ items, pageNumber: 1, totalPages: 1, totalCount: items.length, hasPreviousPage: false, hasNextPage: false })

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('AdminQuestionsPage (specs/076)', () => {
  it('opens on the shop own questions still waiting for an answer', async () => {
    const toAnswer = vi.spyOn(Questions, 'toAnswer').mockResolvedValue(page(question({ answer: null, answeredAt: null })))
    renderAsModerator(<AdminQuestionsPage />, '/admin/questions')

    expect(await screen.findByRole('button', { name: 'Answer' })).toBeInTheDocument()
    expect(toAnswer).toHaveBeenCalledWith(false, 1, 12)
  })

  it('hides only the answer, asking why', async () => {
    const forStaff = vi.spyOn(Questions, 'forStaff').mockResolvedValue(page(question()))
    const hideAnswer = vi.spyOn(Questions, 'hideAnswer').mockResolvedValue(question())
    const user = userEvent.setup()
    renderAsModerator(<AdminQuestionsPage />, '/admin/questions?tab=visible')

    await user.click(await screen.findByRole('button', { name: 'Hide answer' }))
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText('Reason'), 'Sends shoppers elsewhere')
    await user.click(within(dialog).getByRole('button', { name: 'Hide' }))

    await waitFor(() => expect(hideAnswer).toHaveBeenCalledWith('q1', 'Sends shoppers elsewhere'))
    expect(forStaff).toHaveBeenCalledWith(false, 1, 12)
  })

  it('shows why something was hidden and puts it back', async () => {
    vi.spyOn(Questions, 'forStaff').mockResolvedValue(
      page(question({ hiddenAt: '2026-09-22T08:00:00Z', hiddenReason: 'Abuse', answerHiddenAt: '2026-09-22T08:00:00Z', answerHiddenReason: 'Spam' })),
    )
    const restore = vi.spyOn(Questions, 'restore').mockResolvedValue(question())
    const restoreAnswer = vi.spyOn(Questions, 'restoreAnswer').mockResolvedValue(question())
    const user = userEvent.setup()
    renderAsModerator(<AdminQuestionsPage />, '/admin/questions?tab=hidden')

    expect(await screen.findByText('Hidden: Abuse')).toBeInTheDocument()
    expect(screen.getByText('Answer hidden: Spam')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Show question again' }))
    await user.click(screen.getByRole('button', { name: 'Show answer again' }))

    await waitFor(() => expect(restore).toHaveBeenCalledWith('q1'))
    expect(restoreAnswer).toHaveBeenCalledWith('q1')
  })
})
