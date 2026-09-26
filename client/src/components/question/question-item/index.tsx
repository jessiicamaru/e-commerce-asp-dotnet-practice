import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { MessageCircleQuestionIcon, StoreIcon } from 'lucide-react'
import type { Question } from '@/services/question/types'

/**
 * One question and its answer (specs/076), as the product page, a seller's queue and the moderators all draw it.
 * `answerLabel` says who answers - the shop by name, or the shop itself; `showProduct` links to the product for a
 * queue, where questions about many products sit together. What to DO with it goes in `children`.
 */
export function QuestionItem({
  question,
  answerLabel,
  showProduct = false,
  children,
}: {
  question: Question
  answerLabel: string
  showProduct?: boolean
  children?: ReactNode
}) {
  const { t, i18n } = useTranslation('catalog')
  const date = (iso: string) => new Date(iso).toLocaleDateString(i18n.language)

  return (
    <li className="bg-card ring-border/60 grid gap-3 rounded-3xl p-4 ring-1">
      <div className="grid gap-1">
        <div className="text-muted-foreground flex flex-wrap items-center gap-2 text-xs">
          <MessageCircleQuestionIcon className="size-4" />
          <span className="text-foreground font-medium">{question.askerName}</span>
          <span>{date(question.createdAt)}</span>
          {showProduct && question.productName && (
            <Link to={`/products/${question.productId}`} className="hover:text-foreground hover:underline">
              {t('questions.on', { product: question.productName })}
            </Link>
          )}
        </div>
        <p className="text-sm leading-relaxed whitespace-pre-line">{question.body}</p>
        {question.hiddenReason && (
          <p className="text-destructive text-xs">{t('questions.hiddenBecause', { reason: question.hiddenReason })}</p>
        )}
      </div>

      {question.answer !== null && question.answeredAt !== null ? (
        <div className="bg-muted/60 grid gap-1 rounded-2xl p-3">
          <p className="text-muted-foreground flex flex-wrap items-center gap-2 text-xs">
            <StoreIcon className="size-4" />
            <span className="text-foreground font-medium">{answerLabel}</span>
            <span>{date(question.answeredAt)}</span>
            {question.answerEdited && <span>· {t('questions.edited')}</span>}
          </p>
          <p className="text-sm leading-relaxed whitespace-pre-line">{question.answer}</p>
          {question.answerHiddenReason && (
            <p className="text-destructive text-xs">{t('questions.answerHiddenBecause', { reason: question.answerHiddenReason })}</p>
          )}
        </div>
      ) : (
        <p className="text-muted-foreground text-xs">{t('questions.unanswered')}</p>
      )}

      {children}
    </li>
  )
}
