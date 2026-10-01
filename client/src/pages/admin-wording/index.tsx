import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { PageTitle } from '@/components/seller/page-title'
import { NoticeText } from '@/components/shared/notice-text'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { TabStrip } from '@/components/shared/tab-strip'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { useWordingOverview } from '@/hooks/notification-wording'
import type { WordingEntry, WordingKind } from '@/services/notification-wording/types'
import { BUNDLED_WORDING } from '@/utils/notifications/wording'
import { cn } from '@/utils/shared'
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
 *
 * <p>
 * Laid out like the emails page (specs/128, #250): the kinds by name on the left, a language switch, and the chosen
 * kind's sentences - a plural form by form - on the right; the choice is in the address. It listed every sentence of
 * every kind in every language as its own card before, titled with the kind's code: 7,000px.
 * </p>
 */
export function AdminWordingPage() {
  const { t, i18n } = useTranslation('admin')
  const overview = useWordingOverview()
  const [params, setParams] = useSearchParams()
  const [search, setSearch] = useState('')
  const [editing, setEditing] = useState<string | null>(null)

  const kinds = overview.data?.kinds ?? []
  const languages = Object.keys(BUNDLED_WORDING)
  const nameOf = (kind: string) => t(`wording.kindName.${kind}`, { defaultValue: kind })
  const kind = kinds.find((k) => k.kind === params.get('kind')) ?? kinds[0]
  const language =
    languages.find((l) => l === params.get('lang')) ?? languages.find((l) => i18n.language.startsWith(l)) ?? languages[0]
  const go = (next: { kind?: string; lang?: string }) => {
    setEditing(null)
    setParams(new URLSearchParams({ kind: next.kind ?? kind?.kind ?? '', lang: next.lang ?? language }))
  }

  const linesOf = (k: WordingKind, lang: string): WordingLine[] => {
    const bundled = BUNDLED_WORDING[lang]
    // A kind with plural forms in this language is edited form by form: NewReview_one, NewReview_other.
    const keys = Object.keys(bundled).filter((key) => key === k.kind || key.startsWith(`${k.kind}_`))
    return (keys.length > 0 ? keys : [k.kind]).map((key) => ({
      kind: k,
      key,
      language: lang,
      bundled: bundled[key] ?? '',
      entry: overview.data?.entries.find((e) => e.key === key && e.language === lang),
    }))
  }
  const editedIn = (k: WordingKind) =>
    languages.some((lang) => linesOf(k, lang).some((line) => line.entry && !line.entry.isDefault))
  const term = search.trim().toLowerCase()
  const listed = kinds.filter((k) => !term || k.kind.toLowerCase().includes(term) || nameOf(k.kind).toLowerCase().includes(term))

  return (
    <section className="grid gap-6">
      <PageTitle title={t('wording.title')} subtitle={t('wording.subtitle')} />

      {overview.isError ? (
        <ErrorMessage>{t('wording.loadFailed')}</ErrorMessage>
      ) : overview.isPending || !kind ? (
        <LoadingRows />
      ) : (
        <div className="grid items-start gap-6 lg:grid-cols-[18rem_minmax(0,1fr)]">
          <div className="bg-card ring-border/60 grid gap-2 rounded-3xl p-3 ring-1 lg:sticky lg:top-28">
            <Input
              aria-label={t('wording.search')}
              placeholder={t('wording.search')}
              className="h-9 rounded-xl"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
            <nav aria-label={t('wording.kinds')} className="grid max-h-[60vh] gap-0.5 overflow-y-auto">
              {listed.map((k) => (
                <button
                  key={k.kind}
                  type="button"
                  aria-current={k.kind === kind.kind ? 'true' : undefined}
                  onClick={() => go({ kind: k.kind })}
                  className={cn(
                    'flex items-center justify-between gap-2 rounded-xl px-3 py-2 text-left text-sm transition-colors',
                    k.kind === kind.kind ? 'bg-primary text-primary-foreground' : 'hover:bg-secondary',
                  )}
                >
                  <span className="min-w-0 truncate">{nameOf(k.kind)}</span>
                  {editedIn(k) && <span className="text-xs opacity-80">{t('wording.edited')}</span>}
                </button>
              ))}
            </nav>
          </div>

          <div className="grid gap-4">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h2 className="text-lg font-semibold">{nameOf(kind.kind)}</h2>
                <p className="text-muted-foreground font-mono text-xs">{kind.kind}</p>
              </div>
              <TabStrip
                tabs={languages.map((lang) => ({ value: lang, label: t(`emails.language.${lang}`, { defaultValue: lang }) }))}
                current={language}
                onChange={(lang) => go({ lang })}
              />
            </div>
            <ul className="grid gap-2">
              {linesOf(kind, language).map((line) => {
                const id = `${line.key}/${line.language}`
                const edited = line.entry && !line.entry.isDefault ? line.entry.text : null
                return (
                  <li key={id} className="bg-card ring-border/60 grid gap-2 rounded-2xl p-3 ring-1">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <div className="flex flex-wrap items-center gap-2 text-sm">
                        <span className="font-mono text-xs">{line.key}</span>
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
          </div>
        </div>
      )}
    </section>
  )
}
