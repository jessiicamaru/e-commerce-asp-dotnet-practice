import { useTranslation } from 'react-i18next'
import { AnswerForm } from '@/components/question/answer-form'
import { QuestionItem } from '@/components/question/question-item'
import { Pager } from '@/components/shared/pager'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { PAGE_SIZE } from '@/constants/shared'
import { useQuestionQueue } from '@/hooks/question'

/**
 * The questions the caller answers for (specs/076): a seller's own products, or for staff the shop's own - the
 * server decides which from the token. Unanswered ones oldest first, so nobody waits longest; answered ones newest
 * first, each with a way to rewrite it.
 */
export function AnswerQueue({
  answered,
  page,
  onPage,
  answerLabel,
}: {
  answered: boolean
  page: number
  onPage: (page: number) => void
  answerLabel: string
}) {
  const { t } = useTranslation('catalog')
  const queue = useQuestionQueue(answered, page, PAGE_SIZE)

  if (queue.isError) return <ErrorMessage>{t('questions.loadFailed')}</ErrorMessage>
  if (queue.isPending || !queue.data) return <LoadingRows />
  if (queue.data.totalCount === 0) {
    return (
      <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">
        {answered ? t('questions.noneAnswered') : t('questions.noneWaiting')}
      </p>
    )
  }

  return (
    <>
      <ul className="grid gap-3">
        {queue.data.items.map((question) => (
          <QuestionItem key={question.id} question={question} answerLabel={answerLabel} showProduct>
            <AnswerForm question={question} />
          </QuestionItem>
        ))}
      </ul>
      <Pager page={page} pageSize={PAGE_SIZE} totalCount={queue.data.totalCount} onChange={onPage} />
    </>
  )
}
