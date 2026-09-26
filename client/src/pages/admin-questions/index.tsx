import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { AnswerQueue } from '@/components/question/answer-queue'
import { QuestionItem } from '@/components/question/question-item'
import { PageTitle } from '@/components/seller/page-title'
import { Pager } from '@/components/shared/pager'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { TabStrip } from '@/components/shared/tab-strip'
import { PAGE_SIZE } from '@/constants/shared'
import { useStaffQuestions } from '@/hooks/question'
import { ModerationActions } from './moderation-actions'

type Tab = 'answer' | 'visible' | 'hidden'

/**
 * Questions for staff (specs/076): the shop's own products' questions to answer, and every product's to moderate -
 * a question hidden leaves its page with its answer; an answer hidden leaves the question unanswered and locked.
 */
export function AdminQuestionsPage() {
  const { t } = useTranslation('admin')
  const [params, setParams] = useSearchParams()
  const tab = (['visible', 'hidden'] as const).find((value) => value === params.get('tab')) ?? 'answer'
  const page = Number(params.get('page') ?? '1') || 1

  const go = (next: { tab?: Tab; page?: number }) => {
    const merged = new URLSearchParams()
    const target = next.tab ?? tab
    if (target !== 'answer') merged.set('tab', target)
    if (next.page && next.page > 1) merged.set('page', String(next.page))
    setParams(merged)
  }

  return (
    <section className="grid gap-6">
      <PageTitle title={t('questions.title')} subtitle={t('questions.subtitle')} />
      <TabStrip<Tab>
        tabs={[
          { value: 'answer', label: t('questions.tab.answer') },
          { value: 'visible', label: t('questions.tab.visible') },
          { value: 'hidden', label: t('questions.tab.hidden') },
        ]}
        current={tab}
        onChange={(next) => go({ tab: next, page: 1 })}
      />
      {tab === 'answer' ? (
        <AnswerQueue answered={false} page={page} onPage={(next) => go({ page: next })} answerLabel={t('questions.shopsAnswer')} />
      ) : (
        <StaffList hidden={tab === 'hidden'} page={page} onPage={(next) => go({ page: next })} />
      )}
    </section>
  )
}

function StaffList({ hidden, page, onPage }: { hidden: boolean; page: number; onPage: (page: number) => void }) {
  const { t } = useTranslation('admin')
  const list = useStaffQuestions(hidden, page, PAGE_SIZE)

  if (list.isError) return <ErrorMessage>{t('questions.loadFailed')}</ErrorMessage>
  if (list.isPending || !list.data) return <LoadingRows />
  if (list.data.totalCount === 0) return <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">{t('questions.none')}</p>

  return (
    <>
      <ul className="grid gap-3">
        {list.data.items.map((question) => (
          <QuestionItem key={question.id} question={question} answerLabel={t('questions.answerLabel')} showProduct>
            <ModerationActions question={question} />
          </QuestionItem>
        ))}
      </ul>
      <Pager page={page} pageSize={PAGE_SIZE} totalCount={list.data.totalCount} onChange={onPage} />
    </>
  )
}
