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
