import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { StarInput, StarRating } from '@/components/product/star-rating'
import { Pager } from '@/components/shared/pager'
import { ErrorMessage } from '@/components/shared/query-state'
import { ServerError } from '@/components/shared/server-error'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { PAGE_SIZE } from '@/constants/shared'
import { useAuth } from '@/context/auth/useAuth'
import { useMyReview, useProductReviews, useWriteReview } from '@/hooks/review'
import type { Product } from '@/services/product/types'
import type { Review } from '@/services/review/types'

/**
 * What buyers thought of this product (specs/046), and - for somebody who received it - a place to say.
 *
 * <p>
 * Who may write is the server's decision: Catalog knows who received what from Order's delivery event.
 * The form is offered only when it says so, and a refusal is shown in its words. One review each: a
 * second write edits the first, so the form fills with theirs.
 * </p>
 */
export function ProductReviews({ product }: { product: Product }) {
  const { t, i18n } = useTranslation('catalog')
  const { user } = useAuth()
  const [page, setPage] = useState(1)
  const reviews = useProductReviews(product.id, page, PAGE_SIZE)
  const mine = useMyReview(product.id, user !== null)

  return (
    <section id="reviews" className="mt-12 grid gap-6">
      <div className="flex flex-wrap items-baseline gap-3">
        <h2 className="text-xl font-bold">{t('reviews.title')}</h2>
        {product.ratingCount > 0 && product.ratingAverage !== null && (
          <span className="flex items-center gap-2 text-sm">
            <StarRating value={product.ratingAverage} label={t('reviews.average', { average: product.ratingAverage.toFixed(1) })} />
            <span className="font-semibold">{product.ratingAverage.toFixed(1)}</span>
            <span className="text-muted-foreground">{t('reviews.count', { count: product.ratingCount })}</span>
          </span>
        )}
      </div>

      {!user ? (
        <p className="text-muted-foreground text-sm">
          <Link to="/sign-in" className="underline">
            {t('reviews.signIn')}
          </Link>
        </p>
      ) : mine.data?.eligible ? (
        <ReviewForm key={mine.data.review?.id ?? 'new'} productId={product.id} existing={mine.data.review} />
      ) : mine.data ? (
        <p className="text-muted-foreground text-sm">{t('reviews.onlyBuyers')}</p>
      ) : null}

      {reviews.isError ? (
        <ErrorMessage>{t('reviews.loadFailed')}</ErrorMessage>
      ) : reviews.data && reviews.data.totalCount === 0 ? (
        <p className="text-muted-foreground text-sm">{t('reviews.none')}</p>
      ) : (
        reviews.data && (
          <>
            <ul className="grid gap-3">
              {reviews.data.items.map((review) => (
                <ReviewItem key={review.id} review={review} language={i18n.language} />
              ))}
            </ul>
            <Pager page={page} pageSize={PAGE_SIZE} totalCount={reviews.data.totalCount} onChange={setPage} />
          </>
        )
      )}
    </section>
  )
}

function ReviewItem({ review, language }: { review: Review; language: string }) {
  const { t } = useTranslation('catalog')

  return (
    <li className="bg-card ring-border/60 grid gap-1.5 rounded-3xl p-4 ring-1">
      <div className="flex flex-wrap items-center gap-2 text-sm">
        <StarRating value={review.rating} label={t('reviews.starsOf', { count: review.rating })} />
        <span className="font-medium">{review.authorName}</span>
        <span className="text-muted-foreground text-xs">
          {new Date(review.createdAt).toLocaleDateString(language)}
          {review.edited && ` · ${t('reviews.edited')}`}
        </span>
      </div>
      {review.body && <p className="text-sm leading-relaxed whitespace-pre-line">{review.body}</p>}
    </li>
  )
}

function ReviewForm({ productId, existing }: { productId: string; existing: Review | null }) {
  const { t } = useTranslation('catalog')
  const write = useWriteReview(productId)
  const [rating, setRating] = useState(existing?.rating ?? 0)
  const [body, setBody] = useState(existing?.body ?? '')

  const submit = (event: FormEvent) => {
    event.preventDefault()
    // mutateAsync, not mutate's own onSuccess: the first review gives the form a new key, so it remounts before that
    // callback would run - and a callback handed to mutate does not run for a form that is gone (found by the
    // browser tests, specs/080). A refusal is shown by `write.error`; the promise's rejection needs no handling here.
    write.mutateAsync({ rating, body: body.trim() }).then(
      () => toast.success(t('reviews.saved')),
      () => {},
    )
  }

  return (
    <form onSubmit={submit} className="bg-card ring-border/60 grid gap-3 rounded-3xl p-5 ring-1">
      <p className="font-semibold">{existing ? t('reviews.yours') : t('reviews.write')}</p>
      <div className="grid gap-1.5">
        <span className="text-sm font-medium">{t('reviews.rating')}</span>
        <StarInput value={rating} onChange={setRating} labelFor={(n) => t('reviews.stars', { count: n })} />
      </div>
      <div className="grid gap-1.5">
        <Label htmlFor="review-body">{t('reviews.body')}</Label>
        <Textarea id="review-body" maxLength={2000} value={body} onChange={(event) => setBody(event.target.value)} />
      </div>
      <ServerError error={write.error} fallback={t('reviews.failed')} />
      <Button type="submit" className="justify-self-start rounded-full" disabled={rating === 0 || write.isPending}>
        {write.isPending ? t('reviews.saving') : existing ? t('reviews.update') : t('reviews.submit')}
      </Button>
    </form>
  )
}
