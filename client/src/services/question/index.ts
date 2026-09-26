// The model types live in ./types, imported from there: this file's export is the class.
import { http } from '@/config/axios'
import type { Page } from '@/services/product/types'
import type { Question } from './types'

/**
 * Questions about a product (specs/076, #110). Reading a product's is public; asking is a signed-in customer's;
 * answering is the product's seller's - or staff's for the shop's own - which the server decides from the product,
 * never from anything sent here. Hiding is staff's.
 */
export class Questions {
  static async forProduct(productId: string, page: number, pageSize: number): Promise<Page<Question>> {
    const { data } = await http.get<Page<Question>>(`/products/${productId}/questions?pageNumber=${page}&pageSize=${pageSize}`)
    return data
  }

  static async ask(productId: string, body: string): Promise<Question> {
    const { data } = await http.post<Question>(`/products/${productId}/questions`, { body })
    return data
  }

  /** Answers it, or rewrites the answer: one per question. */
  static async answer(id: string, answer: string): Promise<Question> {
    const { data } = await http.put<Question>(`/questions/${id}/answer`, { answer })
    return data
  }

  /** What the caller answers for: a seller's own products, or for staff the shop's own. */
  static async toAnswer(answered: boolean, page: number, pageSize: number): Promise<Page<Question>> {
    const { data } = await http.get<Page<Question>>(`/questions/to-answer?answered=${answered}&pageNumber=${page}&pageSize=${pageSize}`)
    return data
  }

  static async forStaff(hidden: boolean, page: number, pageSize: number): Promise<Page<Question>> {
    const { data } = await http.get<Page<Question>>(`/questions?hidden=${hidden}&pageNumber=${page}&pageSize=${pageSize}`)
    return data
  }

  static async hide(id: string, reason: string): Promise<Question> {
    const { data } = await http.post<Question>(`/questions/${id}/hide`, { reason })
    return data
  }

  static async restore(id: string): Promise<Question> {
    const { data } = await http.post<Question>(`/questions/${id}/restore`)
    return data
  }

  static async hideAnswer(id: string, reason: string): Promise<Question> {
    const { data } = await http.post<Question>(`/questions/${id}/answer/hide`, { reason })
    return data
  }

  static async restoreAnswer(id: string): Promise<Question> {
    const { data } = await http.post<Question>(`/questions/${id}/answer/restore`)
    return data
  }
}
