import { api } from './http'

// The customer's address book (#37), kept by Identity. Order reads the chosen one over gRPC at
// checkout and copies it onto the order, so editing an address here never changes a past order.

export interface AddressFields {
  recipientName: string
  line1: string
  line2: string | null
  city: string
  region: string | null
  postalCode: string
  /** Two-letter ISO 3166 code: it decides the tax rate at checkout (ADR-002). */
  country: string
  phone: string | null
}

export interface Address extends AddressFields {
  id: string
  isDefault: boolean
}

export const listAddresses = () => api<Address[]>('/addresses')

export const createAddress = (fields: AddressFields) => api<Address>('/addresses', { method: 'POST', body: fields })

export const updateAddress = (id: string, fields: AddressFields) =>
  api<Address>(`/addresses/${id}`, { method: 'PUT', body: fields })

export const deleteAddress = (id: string) => api<void>(`/addresses/${id}`, { method: 'DELETE' })

export const makeDefault = (id: string) => api<void>(`/addresses/${id}/default`, { method: 'PUT' })

export const emptyAddress: AddressFields = {
  recipientName: '',
  line1: '',
  line2: null,
  city: '',
  region: null,
  postalCode: '',
  country: '',
  phone: null,
}

/** One line, for a list or a checkout summary. */
export const describe = (a: AddressFields) =>
  [a.line1, a.line2, a.city, a.region, a.postalCode, a.country].filter(Boolean).join(', ')
