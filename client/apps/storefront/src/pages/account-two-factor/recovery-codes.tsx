import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { Button } from '@ecommerce/ui/button'

/** The recovery codes, shown the one time the server gives them out - with a copy and a download to keep them. */
export function RecoveryCodeList({ codes }: { codes: string[] }) {
  const { t } = useTranslation('auth')
  const text = codes.join('\n')

  const copy = () =>
    navigator.clipboard?.writeText(text).then(() => toast.success(t('twoFactor.copied')), () => toast.error(t('twoFactor.copyFailed')))

  const download = () => {
    const url = URL.createObjectURL(new Blob([text + '\n'], { type: 'text/plain' }))
    const link = Object.assign(document.createElement('a'), { href: url, download: 'recovery-codes.txt' })
    link.click()
    URL.revokeObjectURL(url)
  }

  return (
    <div className="grid gap-3">
      <ol className="bg-muted grid grid-cols-2 gap-x-6 gap-y-1 rounded-xl p-4 font-mono text-sm" aria-label={t('twoFactor.codesTitle')}>
        {codes.map((code) => (
          <li key={code}>{code}</li>
        ))}
      </ol>
      <div className="flex gap-2">
        <Button type="button" variant="outline" size="sm" onClick={() => void copy()}>{t('twoFactor.copy')}</Button>
        <Button type="button" variant="outline" size="sm" onClick={download}>{t('twoFactor.download')}</Button>
      </div>
    </div>
  )
}
