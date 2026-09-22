import { render, screen } from '@testing-library/react'
import { AxiosError } from 'axios'
import { describe, expect, it } from 'vitest'
import { ServerError } from '.'

function refusal(status: number, detail?: string) {
  const error = new AxiosError('failed')
  error.response = {
    status,
    data: detail ? { status, detail } : { status },
    statusText: '',
    headers: {},
    config: { headers: {} as never },
  }
  return error
}

describe('ServerError', () => {
  // The whole reason the server sends `detail` outside Development (specs/022) is so this happens.
  it('shows what the server actually said', () => {
    render(<ServerError error={refusal(409, "A product with SKU 'X100' already exists.")} fallback="Failed." />)

    expect(screen.getByRole('alert')).toHaveTextContent("A product with SKU 'X100' already exists.")
  })

  it('shows a validation message a seller can act on', () => {
    render(<ServerError error={refusal(400, '9.99 is not an amount VND can hold.')} fallback="Failed." />)

    expect(screen.getByRole('alert')).toHaveTextContent('9.99 is not an amount VND can hold.')
  })

  // A 500's detail is hidden by the server on purpose; there is nothing to repeat.
  it('falls back for a server fault rather than inventing detail', () => {
    render(<ServerError error={refusal(500, 'Object reference not set')} fallback="Something went wrong." />)

    expect(screen.getByRole('alert')).toHaveTextContent('Something went wrong.')
    expect(screen.queryByText(/Object reference/)).not.toBeInTheDocument()
  })

  it('falls back when the server could not be reached at all', () => {
    render(<ServerError error={new AxiosError('Network Error')} fallback="The shop is unreachable." />)

    expect(screen.getByRole('alert')).toHaveTextContent('The shop is unreachable.')
  })

  it('renders nothing when there is no error', () => {
    render(<ServerError error={null} fallback="Failed." />)

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
