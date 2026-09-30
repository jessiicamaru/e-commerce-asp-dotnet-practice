import { createHmac } from 'node:crypto'

/**
 * What an authenticator app shows (RFC 6238; specs/110): the browser flows sign staff in with a code, as a person does.
 * How it works: docs/features/auth/totp-two-factor.md.
 */

const ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'

export function currentStep(): number {
  return Math.floor(Date.now() / 30_000)
}

export function codeAt(base32: string, step: number): string {
  let bits = ''
  for (const c of base32.replace(/[\s=-]/g, '').toUpperCase()) bits += ALPHABET.indexOf(c).toString(2).padStart(5, '0')
  const key = Buffer.from(bits.match(/.{8}/g)!.map((b) => parseInt(b, 2)))
  const counter = Buffer.alloc(8)
  counter.writeBigUInt64BE(BigInt(step))
  const hash = createHmac('sha1', key).update(counter).digest()
  const offset = hash[hash.length - 1] & 15
  return String((hash.readUInt32BE(offset) & 0x7fffffff) % 1_000_000).padStart(6, '0')
}

/**
 * A code the server will still accept: a code works once, so after one was used in window `lastStep` the next is the
 * window after - accepted up to one window early - and, when even that is too far ahead, this waits for the clock.
 */
export async function freshCode(base32: string, lastStep: number | undefined): Promise<{ code: string; step: number }> {
  while (lastStep !== undefined && lastStep > currentStep()) {
    await new Promise((resolve) => setTimeout(resolve, 1_000))
  }
  const step = lastStep === undefined ? currentStep() : Math.max(currentStep(), lastStep + 1)
  return { code: codeAt(base32, step), step }
}
