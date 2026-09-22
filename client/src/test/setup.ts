import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, beforeEach, vi } from 'vitest'

// Nothing in a unit test may reach the network. A test that forgot to stub a call should FAIL
// loudly here rather than pass slowly against whatever happens to be running on this machine -
// which is how a suite starts depending on a developer's docker compose.
beforeEach(() => {
  vi.stubGlobal(
    'fetch',
    vi.fn(() => {
      throw new Error('A test tried to reach the network. Stub the service call instead.')
    }),
  )
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})
