import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { AnswerForm } from '@/components/question/answer-form'
import { QuestionItem } from '@/components/question/question-item'
import { Pager } from '@/components/shared/pager'
import { ErrorMessage } from '@/components/shared/query-state'
import { ServerError } from '@/components/shared/server-error'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { PAGE_SIZE } from '@/constants/shared'
import { useAuth } from '@/context/auth/useAuth'
import { useAskQuestion, useProductQuestions } from '@/hooks/question'
import type { Product } from '@/services/product/types'

/**
 * Questions about this product and the seller's answers (specs/076, #110), with a place to ask and - for whoever
 * answers for this product - a place to answer.
 *
 * <p>
 * Who answers is drawn from the product (its seller, or staff for the shop's own) and the caller's roles. That is
 * for DRAWING only: the server decides from the same row, and anybody else who tried would get its 404.
 * </p>
 */
export function ProductQuestions({ product }: { product: Product }) {
  const { t } = useTranslation('catalog')
  const { user, isStaff } = useAuth()
  const [page, setPage] = useState(1)
  const questions = useProductQuestions(product.id, page, PAGE_SIZE)

  const ownProduct = user !== null && product.sellerId === user.id
  const mayAnswer = product.sellerId ? ownProduct : isStaff
  const mayAsk = user !== null && !ownProduct && user.roles.includes('Customer')
  const answerLabel = product.sellerName ? t('questions.answerFrom', { shop: product.sellerName }) : t('questions.answerFromShop')

  return (
    <section id="questions" className="mt-12 grid gap-6">
      <h2 className="text-xl font-bold">{t('questions.title')}</h2>

      {!user ? (
        <p className="text-muted-foreground text-sm">
          <Link to="/sign-in" state={{ from: `/products/${product.id}` }} className="underline">
            {t('questions.signIn')}
          </Link>
        </p>
      ) : (
        mayAsk && <AskForm productId={product.id} />
      )}

      {questions.isError ? (
        <ErrorMessage>{t('questions.loadFailed')}</ErrorMessage>
      ) : questions.data && questions.data.totalCount === 0 ? (
        <p className="text-muted-foreground text-sm">{t('questions.none')}</p>
      ) : (
        questions.data && (
          <>
            <ul className="grid gap-3">
              {questions.data.items.map((question) => (
                <QuestionItem key={question.id} question={question} answerLabel={answerLabel}>
                  {mayAnswer && <AnswerForm question={question} />}
                </QuestionItem>
              ))}
            </ul>
            <Pager page={page} pageSize={PAGE_SIZE} totalCount={questions.data.totalCount} onChange={setPage} />
          </>
        )
      )}
    </section>
  )
}

function AskForm({ productId }: { productId: string }) {
  const { t } = useTranslation('catalog')
  const ask = useAskQuestion(productId)
  const [body, setBody] = useState('')

  const submit = (event: FormEvent) => {
    event.preventDefault()
    ask.mutate(body.trim(), {
      onSuccess: () => {
        setBody('')
        toast.success(t('questions.asked'))
      },
    })
  }

  return (
    <form onSubmit={submit} className="bg-card ring-border/60 grid gap-3 rounded-3xl p-5 ring-1">
      <Label htmlFor="question-body">{t('questions.ask')}</Label>
      <Textarea
        id="question-body"
        maxLength={1000}
        placeholder={t('questions.placeholder')}
        value={body}
        onChange={(event) => setBody(event.target.value)}
      />
      <ServerError error={ask.error} fallback={t('questions.failed')} />
      <Button type="submit" className="justify-self-start rounded-full" disabled={!body.trim() || ask.isPending}>
        {ask.isPending ? t('questions.sending') : t('questions.send')}
      </Button>
    </form>
  )
}
