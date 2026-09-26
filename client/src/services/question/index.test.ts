import { describe, expect, it, vi } from 'vitest'
import { http } from '@/config/axios'
import { Questions } from '.'

describe('Questions (specs/076)', () => {
  /** Who asks and who answers come from the token: no user id, no seller id, anywhere in a request. */
  it('asks, answers and moderates through the addresses the server serves, naming nobody', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: {} })
    const post = vi.spyOn(http, 'post').mockResolvedValue({ data: {} })
    const put = vi.spyOn(http, 'put').mockResolvedValue({ data: {} })

    await Questions.forProduct('p1', 2, 12)
    await Questions.toAnswer(false, 1, 12)
    await Questions.forStaff(true, 1, 12)
    await Questions.ask('p1', 'Charger?')
    await Questions.answer('q1', 'Yes.')
    await Questions.hide('q1', 'Abuse')
    await Questions.restore('q1')
    await Questions.hideAnswer('q1', 'Spam')
    await Questions.restoreAnswer('q1')

    expect(get.mock.calls.map((c) => c[0])).toEqual([
      '/products/p1/questions?pageNumber=2&pageSize=12',
      '/questions/to-answer?answered=false&pageNumber=1&pageSize=12',
      '/questions?hidden=true&pageNumber=1&pageSize=12',
    ])
    expect(post.mock.calls).toEqual([
      ['/products/p1/questions', { body: 'Charger?' }],
      ['/questions/q1/hide', { reason: 'Abuse' }],
      ['/questions/q1/restore'],
      ['/questions/q1/answer/hide', { reason: 'Spam' }],
      ['/questions/q1/answer/restore'],
    ])
    expect(put.mock.calls).toEqual([['/questions/q1/answer', { answer: 'Yes.' }]])
  })
})
