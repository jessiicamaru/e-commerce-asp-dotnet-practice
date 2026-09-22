import type { AddressFields } from '@/services/address/types'

/** One line, for a list or a checkout summary. */
export const describeAddress = (address: AddressFields) =>
  [address.line1, address.line2, address.city, address.region, address.postalCode, address.country]
    .filter(Boolean)
    .join(', ')

/** Optional fields go as null rather than "", so an emptied Line 2 is cleared, not stored blank. */
export function normaliseAddress(fields: AddressFields): AddressFields {
  const cleaned = Object.fromEntries(
    Object.entries(fields).map(([key, value]) => [key, typeof value === 'string' && value.trim() === '' ? null : value]),
  ) as unknown as AddressFields

  return { ...cleaned, country: (cleaned.country ?? '').toUpperCase() }
}
