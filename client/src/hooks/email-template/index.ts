import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { EmailTemplates } from '@/services/email-template'
import type { EmailDraft } from '@/services/email-template/types'

export function useEmailTemplates() {
  return useQuery({ queryKey: queryKeys.emailTemplates(), queryFn: () => EmailTemplates.list() })
}

export function useEmailTemplateVersions(template: string, language: string) {
  return useQuery({
    queryKey: queryKeys.emailTemplateVersions(template, language),
    queryFn: () => EmailTemplates.versions(template, language),
  })
}

/**
 * Save, reset and restore for one email in one language. Each makes a new version on top of `expectedVersion`, then
 * re-reads the list and the history - which version is current is the server's to say.
 */
export function useEmailTemplateChanges(template: string, language: string, expectedVersion: number) {
  const queryClient = useQueryClient()
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['email-templates'] })
  return {
    save: useMutation({
      mutationFn: (draft: EmailDraft) => EmailTemplates.save(template, language, draft, expectedVersion),
      onSuccess: refresh,
    }),
    reset: useMutation({ mutationFn: () => EmailTemplates.reset(template, language, expectedVersion), onSuccess: refresh }),
    restore: useMutation({
      mutationFn: (version: number) => EmailTemplates.restore(template, language, version, expectedVersion),
      onSuccess: refresh,
    }),
  }
}

export function useEmailPreview(template: string, language: string) {
  return {
    preview: useMutation({ mutationFn: (draft: EmailDraft) => EmailTemplates.preview(template, language, draft) }),
    test: useMutation({ mutationFn: (draft: EmailDraft) => EmailTemplates.test(template, language, draft) }),
  }
}
