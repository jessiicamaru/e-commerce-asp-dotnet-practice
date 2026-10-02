import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { Button } from '@ecommerce/ui/button'
import { useAuth } from '@ecommerce/core/context/auth/useAuth'
import { useDownloadMyData } from '@ecommerce/core/hooks/me'
import { myDataFileName } from '@ecommerce/core/utils/account'

/**
 * "Download my data" (specs/111): one JSON file from the six services, saved by the browser. Nothing is kept here -
 * the file goes straight to the person, and a service that did not answer is named so they know the file is partial.
 */
export function MyDataCard() {
  const { t } = useTranslation('auth')
  const { user } = useAuth()
  const download = useDownloadMyData()

  const start = () => {
    if (!user) return
    void download.mutateAsync({ id: user.id, email: user.email }).then(({ file, unavailable }) => {
      const now = new Date()
      const url = URL.createObjectURL(new Blob([JSON.stringify(file, null, 2)], { type: 'application/json' }))
      const link = Object.assign(document.createElement('a'), { href: url, download: myDataFileName(now) })
      link.click()
      URL.revokeObjectURL(url)
      if (unavailable.length === 0) toast.success(t('account.myDataDone'))
      else toast.warning(t('account.myDataPartial', { services: unavailable.map((s) => t(`account.myDataServices.${s}`)).join(', ') }))
    })
  }

  return (
    <div className="bg-card ring-border/60 grid gap-4 rounded-3xl p-6 ring-1">
      <div>
        <h2 className="font-semibold">{t('account.myData')}</h2>
        <p className="text-muted-foreground text-sm">{t('account.myDataHint')}</p>
      </div>
      <Button type="button" variant="outline" className="justify-self-start rounded-full" disabled={download.isPending} onClick={start}>
        {download.isPending ? t('account.myDataPreparing') : t('account.myDataDownload')}
      </Button>
    </div>
  )
}
