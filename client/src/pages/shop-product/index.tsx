import { Fragment } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ChevronRightIcon, ExternalLinkIcon, Trash2Icon } from 'lucide-react'
import { toast } from 'sonner'
import { Availability } from '@/components/product/availability'
import { ProductImage } from '@/components/product/product-image'
import { VariantEditor } from '@/components/seller/variant-editor'
import { ImageDropzone } from '@/components/shared/image-dropzone'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { ServerError } from '@/components/shared/server-error'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import { Button, buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Separator } from '@/components/ui/separator'
import { useDeleteProduct, useProductInEveryCurrency, useUploadProductImage } from '@/hooks/product'
import { useVariantStock } from '@/hooks/stock'
import { cn } from '@/utils/shared'
import { ReviewBadge } from '@/components/product/review-badge'
import { ReviewBanner } from '@/components/seller/review-banner'

/**
 * One of the seller's own listings (specs/028, 031, 032): its photograph, and every variant's prices,
 * stock and picture.
 *
 * <p>
 * <b>It is read through the ordinary product endpoint</b>, the same one a shopper uses. The writes are
 * what is guarded, by `SellerOwnership`, which answers <b>404</b> for somebody else's listing - the same
 * answer as a product that does not exist, on purpose. A seller who opens another seller's id sees the
 * page and is refused the moment they change anything, in the server's own words.
 * </p>
 * <p>
 * The prices are read <b>once per currency</b>, because a response carries one currency's prices
 * (specs/022) and the person setting them needs to see both.
 * </p>
 */
export function SellerProductPage() {
  const { t } = useTranslation('seller')
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const product = useProductInEveryCurrency(id)
  const upload = useUploadProductImage(id)
  const remove = useDeleteProduct(id)
  const variants = product.product?.variants ?? []
  const stock = useVariantStock(variants.map((variant) => variant.id))

  if (product.isError) {
    return <ErrorMessage>{t('listing.loadFailed')}</ErrorMessage>
  }

  if (product.isPending || !product.product) {
    return <LoadingRows rows={4} />
  }

  const item = product.product

  return (
    <section className="grid gap-6">
      <nav aria-label={t('menu.products')} className="text-muted-foreground flex items-center gap-1 text-sm">
        <Link to="/shop/products" className="hover:text-foreground">
          {t('menu.products')}
        </Link>
        <ChevronRightIcon className="size-4" />
        <span className="text-foreground truncate">{item.name}</span>
      </nav>

      <header className="flex flex-wrap items-start justify-between gap-4">
        <div className="grid gap-2">
          <h1 className="text-2xl font-bold tracking-tight text-balance">{item.name}</h1>
          <div className="flex flex-wrap items-center gap-2 text-sm">
            <span className="text-muted-foreground font-mono text-xs">{item.sku}</span>
            <Availability value={item.availability} />
            <ReviewBadge status={item.reviewStatus} />
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <Link to={`/products/${item.id}`} className={cn(buttonVariants({ variant: 'outline' }), 'h-9 rounded-full px-4')}>
            <ExternalLinkIcon /> {t('edit.view')}
          </Link>

          <AlertDialog>
            <AlertDialogTrigger
              render={<Button variant="destructive" className="h-9 rounded-full px-4" disabled={remove.isPending} />}
            >
              <Trash2Icon /> {t('edit.withdraw')}
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>{t('edit.withdraw')}</AlertDialogTitle>
                <AlertDialogDescription>{t('edit.withdrawConfirm', { name: item.name })}</AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>{t('action.cancel', { ns: 'common' })}</AlertDialogCancel>
                <AlertDialogAction
                  variant="destructive"
                  onClick={() =>
                    remove.mutate(undefined as never, {
                      onSuccess: () => {
                        toast.success(t('edit.withdrawn', { name: item.name }))
                        navigate('/shop/products')
                      },
                    })
                  }
                >
                  {t('edit.withdraw')}
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </div>
      </header>

      <ReviewBanner product={item} />
      <ServerError error={remove.error} fallback={t('listing.loadFailed')} />

      <div className="grid items-start gap-6 lg:grid-cols-[18rem_1fr]">
        <Card className="rounded-3xl lg:sticky lg:top-28">
          <CardHeader>
            <CardTitle>{t('edit.image')}</CardTitle>
            <CardDescription>{t('edit.imageHint')}</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-2">
            <ImageDropzone
              label={t('edit.image')}
              busy={upload.isPending}
              onFile={(file) => upload.mutate(file, { onSuccess: () => toast.success(t('edit.imageSaved')) })}
              preview={item.imageUrl ? <ProductImage product={item} large /> : undefined}
            />
            <ServerError error={upload.error} fallback={t('listing.loadFailed')} />
          </CardContent>
        </Card>

        <Card className="rounded-3xl">
          <CardHeader>
            <CardTitle>{t('edit.variants')}</CardTitle>
            <CardDescription>{t('edit.variantsHint')}</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-6">
            {variants.map((variant, index) => (
              <Fragment key={variant.id}>
                {index > 0 && <Separator />}
                <VariantEditor
                  product={item}
                  variant={variant}
                  index={index}
                  count={variants.length}
                  prices={Object.fromEntries(
                    Object.entries(product.byCurrency).map(([currency, byVariant]) => [currency, byVariant[variant.id] ?? null]),
                  )}
                  stock={stock.byVariant[variant.id] ?? null}
                  stockPending={stock.isPending}
                />
              </Fragment>
            ))}
          </CardContent>
        </Card>
      </div>
    </section>
  )
}
