import type { UseMutationResult } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { HistoryIcon } from 'lucide-react'
import { ErrorMessage } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { useEmailTemplateVersions } from '@/hooks/email-template'
import type { EmailTemplate } from '@/services/email-template/types'

/**
 * An email's saved versions, newest first (specs/077). Restoring one makes it the newest again - history is never
 * rewritten, so a restore can itself be undone.
 */
export function EmailVersions({
  current,
  restore,
}: {
  current: EmailTemplate
  restore: UseMutationResult<EmailTemplate, Error, number>
}) {
  const { t, i18n } = useTranslation('admin')
  const versions = useEmailTemplateVersions(current.template, current.language)

  return (
    <aside className="bg-card ring-border/60 grid content-start gap-3 rounded-3xl p-4 ring-1">
      <h2 className="flex items-center gap-2 font-semibold">
        <HistoryIcon className="size-4" /> {t('emails.versions')}
      </h2>
      {versions.isError ? (
        <ErrorMessage>{t('emails.loadFailed')}</ErrorMessage>
      ) : versions.data && versions.data.length === 0 ? (
        <p className="text-muted-foreground text-sm">{t('emails.noVersions')}</p>
      ) : (
        <ul className="grid gap-2">
          {versions.data?.map((v) => (
            <li key={v.version} className="flex items-center justify-between gap-2 text-sm">
              <div className="grid min-w-0">
                <span className="font-medium">
                  {t('emails.versionLabel', { version: v.version })}
                  {v.isDefault && <span className="text-muted-foreground font-normal"> · {t('emails.builtIn')}</span>}
                </span>
                <span className="text-muted-foreground truncate text-xs">{new Date(v.createdAt).toLocaleString(i18n.language)}</span>
              </div>
              {v.version === current.version ? (
                <span className="text-muted-foreground text-xs">{t('emails.currentVersion')}</span>
              ) : (
                <Button
                  size="sm"
                  variant="outline"
                  className="rounded-full px-3"
                  disabled={restore.isPending}
                  onClick={() => restore.mutate(v.version, { onSuccess: () => toast.success(t('emails.restored', { version: v.version })) })}
                >
                  {t('emails.restore')}
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}
    </aside>
  )
}
