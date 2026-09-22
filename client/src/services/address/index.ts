// The model types live in ./types, imported from there: this file's export is the class, and a
// class and an interface cannot share a name.
import { http } from '@/config/axios'
import type { Address as AddressModel, AddressFields } from './types'

/**
 * The customer's address book, kept by Identity. Order reads the chosen one over gRPC at checkout and
 * copies it onto the order, so editing an address here never changes a past order (specs/011).
 */
export class Address {
  static async list(): Promise<AddressModel[]> {
    const { data } = await http.get<AddressModel[]>('/addresses')
    return data
  }

  static async create(fields: AddressFields): Promise<AddressModel> {
    const { data } = await http.post<AddressModel>('/addresses', fields)
    return data
  }

  static async update(id: string, fields: AddressFields): Promise<AddressModel> {
    const { data } = await http.put<AddressModel>(`/addresses/${id}`, fields)
    return data
  }

  static async remove(id: string): Promise<void> {
    await http.delete(`/addresses/${id}`)
  }

  static async makeDefault(id: string): Promise<void> {
    await http.put(`/addresses/${id}/default`)
  }
}
