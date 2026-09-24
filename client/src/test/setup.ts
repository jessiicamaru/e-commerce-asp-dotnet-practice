import '@testing-library/jest-dom/vitest'
import { cleanup, configure } from '@testing-library/react'
import { afterEach, beforeEach, vi } from 'vitest'

// How long findBy/waitFor wait. The default second is enough for one file on its own, but the first render
// in a file pays for i18n and the module graph while every other file runs in parallel - and with fifty
// files that first wait crossed a second on a busy machine, failing tests that pass alone.
configure({ asyncUtilTimeout: 3000 })

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
