import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { AnswerQueue } from '@/components/question/answer-queue'
import { PageTitle } from '@/components/seller/page-title'
import { TabStrip } from '@/components/shared/tab-strip'

/**
 * What shoppers asked about a seller's products (specs/076, #110), and where the seller answers. The answer is
 * public on the product page, written as the shop.
 */
export function ShopQuestionsPage() {
  const { t } = useTranslation('seller')
  const [params, setParams] = useSearchParams()
  const answered = params.get('tab') === 'answered'
  const page = Number(params.get('page') ?? '1') || 1

  const go = (next: { answered?: boolean; page?: number }) => {
    const merged = new URLSearchParams()
    if (next.answered ?? answered) merged.set('tab', 'answered')
    if (next.page && next.page > 1) merged.set('page', String(next.page))
    setParams(merged)
  }

  return (
    <section className="grid gap-6">
      <PageTitle title={t('questions.title')} subtitle={t('questions.subtitle')} />
      <TabStrip
        tabs={[
          { value: 'open', label: t('questions.tab.open') },
          { value: 'answered', label: t('questions.tab.answered') },
        ]}
        current={answered ? 'answered' : 'open'}
        onChange={(tab) => go({ answered: tab === 'answered', page: 1 })}
      />
      <AnswerQueue answered={answered} page={page} onPage={(next) => go({ page: next })} answerLabel={t('questions.yourAnswer')} />
    </section>
  )
}
