import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { SpecificationsTable } from '.'

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('SpecificationsTable (specs/159)', () => {
  it('lists each specification with its value, in the order the server sent', () => {
    render(
      <SpecificationsTable
        specifications={[
          { specificationId: 's1', name: 'Brand', kind: 'Choice', optionId: 'o1', text: null, value: 'Sony' },
          { specificationId: 's2', name: 'Resolution', kind: 'Text', optionId: null, text: '33 MP', value: '33 MP' },
        ]}
      />,
    )

    expect(screen.getByRole('heading', { name: 'Specifications' })).toBeInTheDocument()
    const terms = screen.getAllByRole('term').map((t) => [t.textContent, t.nextElementSibling?.textContent])
    expect(terms).toEqual([['Brand', 'Sony'], ['Resolution', '33 MP']])
  })

  it('draws nothing for a product without any', () => {
    const { container } = render(<SpecificationsTable specifications={[]} />)

    expect(container).toBeEmptyDOMElement()
  })
})
