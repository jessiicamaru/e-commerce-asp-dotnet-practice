import { useId, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { RotateCcwIcon, SaveIcon } from 'lucide-react'
import { NoticeText } from '@/components/shared/notice-text'
import { RichTextEditor } from '@/components/shared/rich-text-editor'
import { ServerError } from '@/components/shared/server-error'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { ApiError } from '@/config/axios'
import { useWordingChanges, useWordingVersions } from '@/hooks/notification-wording'
import { escapeHtml } from '@/utils/notifications'
import { fillSample } from '@/utils/notifications/wording'
import type { WordingLine } from '.'

/** Made-up values for the sample beside the editor - escaped, as real ones are. */
const SAMPLE: Record<string, string> = {
  order: '01a0dd2b',
  total: '₫1,250,000',
  amount: '₫350,000',
  tracking: 'VN-12345',
  shop: 'Lens House',
  by: 'you',
  product: 'Fujifilm X-T5',
  reason: 'blurred photos',
  rating: '5',
  count: '5',
  until: '1/10/2026, 14:30',
}

/**
 * One sentence being edited (specs/078): the inline editor with its kind's placeholders, a live sample, save, reset
 * and the versions. Remounted (by its key) when the version it opened changes.
 */
export function WordingEditor({ line }: { line: WordingLine }) {
  const { t, i18n } = useTranslation('admin')
  const id = useId()
  const current = line.entry && !line.entry.isDefault && line.entry.text !== null ? line.entry.text : line.bundled
  const [text, setText] = useState(current)
  const version = line.entry?.version ?? 0
  const changes = useWordingChanges(line.key, line.language, version)
  const versions = useWordingVersions(line.key, line.language)
  const edited = line.entry !== undefined && !line.entry.isDefault
  const failure = changes.save.error ?? changes.reset.error ?? changes.restore.error
  const messages = failure ? Object.values(ApiError.from(failure).problem.errors ?? {}).flat() : []
  const sample = Object.fromEntries(Object.entries(SAMPLE).map(([key, value]) => [key, escapeHtml(value)]))

  return (
    <div className="grid gap-3 pt-2">
      <Label htmlFor={id}>{t('wording.words')}</Label>
      <RichTextEditor
        id={id}
        variant="inline"
        value={current}
        onChange={setText}
        placeholders={line.kind.placeholders}
        token={(name) => `{{${name}}}`}
      />
      <p className="text-sm">
        <span className="text-muted-foreground">{t('wording.sample')}: </span>
        <NoticeText html={fillSample(text, sample)} links />
      </p>

      {messages.length > 0 ? (
        <ul role="alert" className="text-destructive grid gap-1 text-sm">
          {messages.map((message) => (
            <li key={message}>{message}</li>
          ))}
        </ul>
      ) : (
        <ServerError error={failure} fallback={t('wording.failed')} />
      )}

      <div className="flex flex-wrap gap-2">
        <Button
          size="sm"
          className="rounded-full px-4"
          disabled={text === current || !text.trim() || changes.save.isPending}
          onClick={() => changes.save.mutateAsync(text).then(() => toast.success(t('wording.saved')), () => {})}
        >
          <SaveIcon /> {t('wording.save')}
        </Button>
        <Button
          size="sm"
          variant="ghost"
          className="rounded-full px-3"
          disabled={!edited || changes.reset.isPending}
          onClick={() => changes.reset.mutateAsync(undefined).then(() => toast.success(t('wording.wasReset')), () => {})}
        >
          <RotateCcwIcon /> {t('wording.reset')}
        </Button>
      </div>

      {versions.data && versions.data.length > 0 && (
        <details className="text-sm">
          <summary className="cursor-pointer">{t('wording.versions', { count: versions.data.length })}</summary>
          <ul className="mt-2 grid gap-1.5">
            {versions.data.map((v) => (
              <li key={v.version} className="flex flex-wrap items-center justify-between gap-2">
                <span className="min-w-0">
                  <span className="font-medium">v{v.version}</span>{' '}
                  <span className="text-muted-foreground text-xs">{new Date(v.updatedAt).toLocaleString(i18n.language)}</span>{' '}
                  {v.isDefault ? (
                    <span className="text-muted-foreground">{t('wording.bundled')}</span>
                  ) : (
                    <NoticeText html={v.text ?? ''} links />
                  )}
                </span>
                {v.version !== version && (
                  <Button
                    size="sm"
                    variant="outline"
                    className="h-7 rounded-full px-2.5"
                    disabled={changes.restore.isPending}
                    onClick={() => changes.restore.mutateAsync(v.version).then(() => toast.success(t('wording.restored', { version: v.version })), () => {})}
                  >
                    {t('wording.restore')}
                  </Button>
                )}
              </li>
            ))}
          </ul>
        </details>
      )}
    </div>
  )
}
