import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { Questions } from '@/services/question'

export function useProductQuestions(productId: string, page: number, pageSize: number) {
  return useQuery({
    queryKey: queryKeys.productQuestions(productId, page),
    queryFn: () => Questions.forProduct(productId, page, pageSize),
    placeholderData: (previous) => previous,
    enabled: productId !== '',
  })
}

export function useQuestionQueue(answered: boolean, page: number, pageSize: number) {
  return useQuery({
    queryKey: queryKeys.questionQueue(answered, page),
    queryFn: () => Questions.toAnswer(answered, page, pageSize),
    placeholderData: (previous) => previous,
  })
}

export function useStaffQuestions(hidden: boolean, page: number, pageSize: number) {
  return useQuery({
    queryKey: queryKeys.staffQuestions(hidden, page),
    queryFn: () => Questions.forStaff(hidden, page, pageSize),
    placeholderData: (previous) => previous,
  })
}

/** Every change re-reads every list of questions: which ones it moved between is the server's to say. */
function useRefresh() {
  const queryClient = useQueryClient()
  return async () => {
    await queryClient.invalidateQueries({ queryKey: ['questions'] })
    await queryClient.invalidateQueries({ queryKey: ['my-decisions'] })
  }
}

export function useAskQuestion(productId: string) {
  const refresh = useRefresh()
  return useMutation({ mutationFn: (body: string) => Questions.ask(productId, body), onSuccess: refresh })
}

export function useAnswerQuestion(id: string) {
  const refresh = useRefresh()
  return useMutation({ mutationFn: (answer: string) => Questions.answer(id, answer), onSuccess: refresh })
}

/** Staff's four moves on one question, each shaped for a `TextPrompt` or a plain button. */
export function useQuestionModeration(id: string) {
  const refresh = useRefresh()
  return {
    hide: useMutation({ mutationFn: (reason: string) => Questions.hide(id, reason), onSettled: refresh }),
    restore: useMutation({ mutationFn: () => Questions.restore(id), onSettled: refresh }),
    hideAnswer: useMutation({ mutationFn: (reason: string) => Questions.hideAnswer(id, reason), onSettled: refresh }),
    restoreAnswer: useMutation({ mutationFn: () => Questions.restoreAnswer(id), onSettled: refresh }),
  }
}
