import { describe, expect, it, vi } from 'vitest'
import { http } from '@/config/axios'
import { EmailTemplates } from '.'

describe('EmailTemplates (specs/077)', () => {
  it('reads, saves on top of the version it opened, resets, restores, previews and tests', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: [] })
    const put = vi.spyOn(http, 'put').mockResolvedValue({ data: {} })
    const post = vi.spyOn(http, 'post').mockResolvedValue({ data: {} })
    const draft = { subject: 'Paid: {order}', bodyHtml: '<p>{name}</p>' }

    await EmailTemplates.list()
    await EmailTemplates.versions('OrderPaid', 'vi')
    await EmailTemplates.save('OrderPaid', 'vi', draft, 3)
    await EmailTemplates.reset('OrderPaid', 'vi', 4)
    await EmailTemplates.restore('OrderPaid', 'vi', 2, 5)
    await EmailTemplates.preview('OrderPaid', 'vi', draft)
    await EmailTemplates.test('OrderPaid', 'vi', draft)

    expect(get.mock.calls.map((c) => c[0])).toEqual(['/email-templates', '/email-templates/OrderPaid/vi/versions'])
    expect(put.mock.calls).toEqual([['/email-templates/OrderPaid/vi', { ...draft, expectedVersion: 3 }]])
    expect(post.mock.calls).toEqual([
      ['/email-templates/OrderPaid/vi/reset', { expectedVersion: 4 }],
      ['/email-templates/OrderPaid/vi/versions/2/restore', { expectedVersion: 5 }],
      ['/email-templates/OrderPaid/vi/preview', draft],
      ['/email-templates/OrderPaid/vi/test', draft],
    ])
  })
})
