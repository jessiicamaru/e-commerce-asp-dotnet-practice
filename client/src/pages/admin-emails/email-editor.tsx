import { useId, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { EyeIcon, RotateCcwIcon, SaveIcon, SendIcon } from 'lucide-react'
import { RichTextEditor } from '@/components/shared/rich-text-editor'
import { ServerError } from '@/components/shared/server-error'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ApiError } from '@/config/axios'
import { useEmailPreview, useEmailTemplateChanges } from '@/hooks/email-template'
import type { EmailTemplate } from '@/services/email-template/types'
import { EmailVersions } from './email-versions'

/**
 * One email in one language: its subject and body, what it may use, and what can be done with the draft. Opened
 * from the server's current version, and remounted (by its key) whenever that version changes.
 */
export function EmailEditor({ current }: { current: EmailTemplate }) {
  const { t, i18n } = useTranslation('admin')
  const subjectId = useId()
  const bodyId = useId()
  const subjectRef = useRef<HTMLInputElement>(null)
  const [subject, setSubject] = useState(current.subject)
  const [bodyHtml, setBodyHtml] = useState(current.bodyHtml)
  const changes = useEmailTemplateChanges(current.template, current.language, current.version)
  const drafts = useEmailPreview(current.template, current.language)
  const draft = { subject, bodyHtml }
  const dirty = subject !== current.subject || bodyHtml !== current.bodyHtml

  /** A placeholder into the subject where the cursor is - the body has its own buttons, in the editor. */
  const insertIntoSubject = (name: string) => {
    const input = subjectRef.current
    const at = input?.selectionStart ?? subject.length
    setSubject(subject.slice(0, at) + `{${name}}` + subject.slice(input?.selectionEnd ?? at))
  }

  const failure = changes.save.error ?? changes.reset.error ?? changes.restore.error ?? drafts.preview.error ?? drafts.test.error

  return (
    <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_22rem]">
      <div className="grid content-start gap-5">
        <p className="text-muted-foreground flex flex-wrap items-center gap-2 text-sm">
          {current.isDefault ? (
            <Badge variant="secondary">{t('emails.builtIn')}</Badge>
          ) : (
            <Badge>{t('emails.edited')}</Badge>
          )}
          {current.version > 0 && current.updatedAt && (
            <span>{t('emails.version', { version: current.version, date: new Date(current.updatedAt).toLocaleString(i18n.language) })}</span>
          )}
        </p>

        <div className="grid gap-2">
          <Label htmlFor={subjectId}>{t('emails.subject')}</Label>
          <Input
            id={subjectId}
            ref={subjectRef}
            maxLength={200}
            className="h-10 rounded-xl"
            value={subject}
            onChange={(event) => setSubject(event.target.value)}
          />
          <div className="flex flex-wrap gap-1.5">
            {current.placeholders.map((name) => (
              <Button
                key={name}
                type="button"
                size="sm"
                variant="outline"
                className="h-7 rounded-full px-2.5 font-mono text-xs"
                aria-label={t('emails.insertInSubject', { placeholder: `{${name}}` })}
                onClick={() => insertIntoSubject(name)}
              >
                {`{${name}}`}
              </Button>
            ))}
          </div>
        </div>

        <div className="grid gap-2">
          <Label htmlFor={bodyId}>{t('emails.body')}</Label>
          <RichTextEditor id={bodyId} value={current.bodyHtml} onChange={setBodyHtml} placeholders={current.placeholders} />
          {current.required.length > 0 && (
            <p className="text-muted-foreground text-xs">
              {t('emails.required', { placeholders: current.required.map((name) => `{${name}}`).join(', ') })}
            </p>
          )}
        </div>

        <Problems error={failure} fallback={t('emails.failed')} />

        <div className="flex flex-wrap gap-2">
          <Button
            className="rounded-full"
            disabled={!dirty || changes.save.isPending}
            onClick={() => changes.save.mutate(draft, { onSuccess: () => toast.success(t('emails.saved')) })}
          >
            <SaveIcon /> {t('emails.save')}
          </Button>
          <Button variant="outline" className="rounded-full" disabled={drafts.preview.isPending} onClick={() => drafts.preview.mutate(draft)}>
            <EyeIcon /> {t('emails.preview')}
          </Button>
          <Button
            variant="outline"
            className="rounded-full"
            disabled={drafts.test.isPending}
            onClick={() => drafts.test.mutate(draft, { onSuccess: () => toast.success(t('emails.testSent')) })}
          >
            <SendIcon /> {t('emails.sendTest')}
          </Button>
          <Button
            variant="ghost"
            className="rounded-full"
            disabled={current.isDefault || changes.reset.isPending}
            onClick={() => changes.reset.mutate(undefined, { onSuccess: () => toast.success(t('emails.wasReset')) })}
          >
            <RotateCcwIcon /> {t('emails.reset')}
          </Button>
        </div>

        {drafts.preview.data && (
          <div className="grid gap-2">
            <p className="text-sm">
              <span className="text-muted-foreground">{t('emails.subject')}: </span>
              <span className="font-medium">{drafts.preview.data.subject}</span>
            </p>
            {/* The server's rendering, sandboxed: nothing in it can run or reach the page. */}
            <iframe
              title={t('emails.previewTitle')}
              sandbox=""
              srcDoc={drafts.preview.data.html}
              className="ring-border/60 h-80 w-full rounded-2xl bg-white ring-1"
            />
            <details className="text-sm">
              <summary className="cursor-pointer">{t('emails.plainText')}</summary>
              <pre className="bg-muted mt-2 rounded-xl p-3 text-xs whitespace-pre-wrap">{drafts.preview.data.text}</pre>
            </details>
          </div>
        )}
      </div>

      <EmailVersions current={current} restore={changes.restore} />
    </div>
  )
}

/** Every refusal the server gave - a draft can break two rules at once (a subject and a body) - or its one sentence. */
function Problems({ error, fallback }: { error: unknown; fallback: string }) {
  if (!error) return null
  const messages = Object.values(ApiError.from(error).problem.errors ?? {}).flat()
  if (messages.length === 0) return <ServerError error={error} fallback={fallback} />
  return (
    <ul role="alert" className="text-destructive grid gap-1 text-sm">
      {messages.map((message) => (
        <li key={message}>{message}</li>
      ))}
    </ul>
  )
}
