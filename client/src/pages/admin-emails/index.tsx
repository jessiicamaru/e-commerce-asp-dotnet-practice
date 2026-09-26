import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { PageTitle } from '@/components/seller/page-title'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { TabStrip } from '@/components/shared/tab-strip'
import { useEmailTemplates } from '@/hooks/email-template'
import { EmailEditor } from './email-editor'

/**
 * The words of the shop's emails, for administrators (specs/077, #150): pick an email and a language, edit its
 * subject and body, preview it with made-up data, send it to yourself, save it - and undo any of that from its
 * versions. The server sanitises, checks placeholders and keeps the history; this page draws what it says.
 */
export function AdminEmailsPage() {
  const { t } = useTranslation('admin')
  const [params, setParams] = useSearchParams()
  const templates = useEmailTemplates()

  const names = [...new Set(templates.data?.map((e) => e.template) ?? [])]
  const languages = [...new Set(templates.data?.map((e) => e.language) ?? [])]
  const template = names.find((name) => name === params.get('template')) ?? names[0]
  const language = languages.find((lang) => lang === params.get('lang')) ?? languages[0]
  const current = templates.data?.find((e) => e.template === template && e.language === language)

  const go = (next: { template?: string; lang?: string }) =>
    setParams(new URLSearchParams({ template: next.template ?? template ?? '', lang: next.lang ?? language ?? '' }))

  return (
    <section className="grid gap-6">
      <PageTitle title={t('emails.title')} subtitle={t('emails.subtitle')} />

      {templates.isError ? (
        <ErrorMessage>{t('emails.loadFailed')}</ErrorMessage>
      ) : !templates.data || !current ? (
        <LoadingRows />
      ) : (
        <>
          <div className="flex flex-wrap gap-3">
            <TabStrip
              tabs={names.map((name) => ({ value: name, label: t(`emails.template.${name}`, { defaultValue: name }) }))}
              current={current.template}
              onChange={(next) => go({ template: next })}
            />
            <TabStrip
              tabs={languages.map((lang) => ({ value: lang, label: t(`emails.language.${lang}`, { defaultValue: lang }) }))}
              current={current.language}
              onChange={(next) => go({ lang: next })}
            />
          </div>
          {/* A new key per email and per version: switching, saving, resetting or restoring starts from the server's words. */}
          <EmailEditor key={`${current.template}-${current.language}-${current.version}`} current={current} />
        </>
      )}
    </section>
  )
}
