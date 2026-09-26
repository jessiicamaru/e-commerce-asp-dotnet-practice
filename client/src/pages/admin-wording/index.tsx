import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { PageTitle } from '@/components/seller/page-title'
import { NoticeText } from '@/components/shared/notice-text'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { useWordingOverview } from '@/hooks/notification-wording'
import type { WordingEntry, WordingKind } from '@/services/notification-wording/types'
import { BUNDLED_WORDING } from '@/utils/notifications/wording'
import { WordingEditor } from './wording-editor'

/** One sentence to edit: a kind's key (with its plural form, if any) in one language. */
export interface WordingLine {
  kind: WordingKind
  key: string
  language: string
  bundled: string
  entry: WordingEntry | undefined
}

/**
 * What the notifications say, for administrators (specs/078, #150): every kind's sentence in both languages, the
 * storefront's own words or an edit, and an editor limited to what a notice can show. The server checks placeholders
 * and keeps the history; an edit reaches every reader's bell on their next load.
 */
export function AdminWordingPage() {
  const { t } = useTranslation('admin')
  const overview = useWordingOverview()
  const [search, setSearch] = useState('')
  const [editing, setEditing] = useState<string | null>(null)

  const lines = (overview.data?.kinds ?? []).flatMap((kind) =>
    Object.keys(BUNDLED_WORDING).flatMap((language) => {
      const bundled = BUNDLED_WORDING[language]
      // A kind with plural forms in this language is edited form by form: NewReview_one, NewReview_other.
      const keys = Object.keys(bundled).filter((key) => key === kind.kind || key.startsWith(`${kind.kind}_`))
      return (keys.length > 0 ? keys : [kind.kind]).map<WordingLine>((key) => ({
        kind,
        key,
        language,
        bundled: bundled[key] ?? '',
        entry: overview.data?.entries.find((e) => e.key === key && e.language === language),
      }))
    }),
  )
  const shown = lines.filter((line) => line.key.toLowerCase().includes(search.trim().toLowerCase()))

  return (
    <section className="grid gap-6">
      <PageTitle title={t('wording.title')} subtitle={t('wording.subtitle')} />
      <Input
        aria-label={t('wording.search')}
        placeholder={t('wording.search')}
        className="h-10 max-w-xs rounded-xl"
        value={search}
        onChange={(event) => setSearch(event.target.value)}
      />

      {overview.isError ? (
        <ErrorMessage>{t('wording.loadFailed')}</ErrorMessage>
      ) : overview.isPending ? (
        <LoadingRows />
      ) : (
        <ul className="grid gap-2">
          {shown.map((line) => {
            const id = `${line.key}/${line.language}`
            const edited = line.entry && !line.entry.isDefault ? line.entry.text : null
            return (
              <li key={id} className="bg-card ring-border/60 grid gap-2 rounded-2xl p-3 ring-1">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex flex-wrap items-center gap-2 text-sm">
                    <span className="font-mono text-xs">{line.key}</span>
                    <Badge variant="outline">{line.language}</Badge>
                    {edited !== null && <Badge>{t('wording.edited')}</Badge>}
                  </div>
                  <Button
                    size="sm"
                    variant="outline"
                    className="rounded-full px-3"
                    aria-expanded={editing === id}
                    aria-label={t('wording.editKey', { key: line.key, language: line.language })}
                    onClick={() => setEditing(editing === id ? null : id)}
                  >
                    {editing === id ? t('wording.close') : t('wording.edit')}
                  </Button>
                </div>
                <p className="text-muted-foreground text-sm">
                  <NoticeText html={edited ?? line.bundled} links />
                </p>
                {editing === id && <WordingEditor key={`${id}-${line.entry?.version ?? 0}`} line={line} />}
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}
