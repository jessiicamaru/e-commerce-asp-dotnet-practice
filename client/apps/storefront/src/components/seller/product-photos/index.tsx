import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ArrowLeftIcon, ArrowRightIcon, ImagePlusIcon, LoaderCircleIcon, StarIcon, Trash2Icon } from 'lucide-react'
import { toast } from 'sonner'
import { ProductImage } from '@ecommerce/core/components/product/product-image'
import { ServerError } from '@ecommerce/core/components/shared/server-error'
import { Badge } from '@ecommerce/ui/badge'
import { Button } from '@ecommerce/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@ecommerce/ui/card'
import {
  useAddProductPhoto,
  useMakeProductCover,
  useRemoveProductPhoto,
  useReorderProductPhotos,
  useUploadProductImage,
} from '@ecommerce/core/hooks/product'
import type { Product } from '@ecommerce/core/services/product/types'
import { ImageDropzone } from '@/components/shared/image-dropzone'
import { IMAGE_TYPES, imageProblem } from '@/components/shared/image-dropzone/image-problem'

/** The cover and 9 more (specs/160 research D4) - the server's limit, checked here only to spare a round trip. */
export const MAX_PHOTOS = 10

/**
 * A product's photographs on its seller's page (specs/160, #368): the cover, which a drop replaces as before
 * (specs/019), then the gallery in order - add several at once, move one earlier or later, make one the cover, remove
 * one. Every change to an approved product sends it back to review, and the hint says so before it happens.
 */
export function ProductPhotosCard({ product }: { product: Product }) {
  const { t } = useTranslation('seller')
  const input = useRef<HTMLInputElement>(null)
  const [progress, setProgress] = useState<{ done: number; total: number } | null>(null)

  const replaceCover = useUploadProductImage(product.id)
  const add = useAddProductPhoto(product.id)
  const remove = useRemoveProductPhoto(product.id)
  const makeCover = useMakeProductCover(product.id)
  const reorder = useReorderProductPhotos(product.id)

  const photos = product.photos ?? []
  const count = photos.length + (product.imageUrl ? 1 : 0)
  const busy = !!progress || remove.isPending || makeCover.isPending || reorder.isPending
  const error = add.error ?? remove.error ?? makeCover.error ?? reorder.error ?? replaceCover.error

  async function addFiles(files: File[]) {
    const fine = files.filter((file) => !imageProblem(file))
    const room = MAX_PHOTOS - count
    const taken = fine.slice(0, Math.max(room, 0))

    if (fine.length < files.length) toast.warning(t('photos.skipped', { count: files.length - fine.length }))
    if (taken.length < fine.length) toast.warning(t('photos.full', { max: MAX_PHOTOS }))
    if (taken.length === 0) return

    // One at a time, in the order chosen: the gallery keeps the order they were added in.
    setProgress({ done: 0, total: taken.length })
    let added = 0
    try {
      for (const file of taken) {
        await add.mutateAsync(file)
        added += 1
        setProgress({ done: added, total: taken.length })
      }
    } catch {
      // Shown below by ServerError; what was added before it stays added.
    } finally {
      setProgress(null)
      if (added > 0) toast.success(t('photos.added', { count: added }))
    }
  }

  function move(index: number, by: -1 | 1) {
    const ids = photos.map((photo) => photo.id)
    const [moved] = ids.splice(index, 1)
    ids.splice(index + by, 0, moved)
    reorder
      .mutateAsync(ids)
      .then(() => toast.success(t('photos.reordered')))
      .catch(() => {})
  }

  return (
    <Card className="rounded-3xl">
      <CardHeader>
        <div className="flex items-center justify-between gap-2">
          <CardTitle>{t('photos.title')}</CardTitle>
          <Badge variant="secondary">{t('photos.count', { count, max: MAX_PHOTOS })}</Badge>
        </div>
        <CardDescription>{t('photos.hint', { max: MAX_PHOTOS })}</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4">
        <div className="relative">
          <ImageDropzone
            label={t('edit.image')}
            busy={replaceCover.isPending}
            onFile={(file) =>
              replaceCover
                .mutateAsync(file)
                .then(() => toast.success(t('edit.imageSaved')))
                .catch(() => {})
            }
            preview={product.imageUrl ? <ProductImage product={product} large /> : undefined}
          />
          {product.imageUrl && <Badge className="absolute top-3 left-3">{t('photos.cover')}</Badge>}
        </div>

        {photos.length > 0 ? (
          <ul className="grid grid-cols-2 gap-3">
            {photos.map((photo, index) => {
              const n = index + 2 // the cover is the first
              return (
                <li key={photo.id} className="grid gap-1">
                  <ProductImage product={product} imageUrl={photo.url} thumb />
                  <div className="flex justify-center gap-0.5">
                    <Button
                      size="icon-xs"
                      variant="ghost"
                      aria-label={t('photos.moveEarlier', { n })}
                      disabled={busy || index === 0}
                      onClick={() => move(index, -1)}
                    >
                      <ArrowLeftIcon />
                    </Button>
                    <Button
                      size="icon-xs"
                      variant="ghost"
                      aria-label={t('photos.makeCover', { n })}
                      disabled={busy}
                      onClick={() =>
                        makeCover
                          .mutateAsync(photo.id)
                          .then(() => toast.success(t('photos.coverChanged')))
                          .catch(() => {})
                      }
                    >
                      <StarIcon />
                    </Button>
                    <Button
                      size="icon-xs"
                      variant="ghost"
                      aria-label={t('photos.moveLater', { n })}
                      disabled={busy || index === photos.length - 1}
                      onClick={() => move(index, 1)}
                    >
                      <ArrowRightIcon />
                    </Button>
                    <Button
                      size="icon-xs"
                      variant="ghost"
                      aria-label={t('photos.remove', { n })}
                      disabled={busy}
                      onClick={() =>
                        remove
                          .mutateAsync(photo.id)
                          .then(() => toast.success(t('photos.removed')))
                          .catch(() => {})
                      }
                    >
                      <Trash2Icon />
                    </Button>
                  </div>
                </li>
              )
            })}
          </ul>
        ) : (
          product.imageUrl && <p className="text-muted-foreground text-sm">{t('photos.none')}</p>
        )}

        <Button
          variant="outline"
          disabled={busy || count >= MAX_PHOTOS}
          onClick={() => input.current?.click()}
        >
          {progress ? <LoaderCircleIcon className="animate-spin" /> : <ImagePlusIcon />}
          {progress ? t('photos.adding', progress) : t('photos.add')}
        </Button>
        <input
          ref={input}
          type="file"
          multiple
          accept={IMAGE_TYPES.join(',')}
          className="sr-only"
          tabIndex={-1}
          aria-hidden="true"
          data-testid="add-photos"
          onChange={(event) => {
            const files = Array.from(event.target.files ?? [])
            event.target.value = '' // so choosing the same files again still fires
            void addFiles(files)
          }}
        />

        <ServerError error={error} fallback={t('listing.loadFailed')} />
      </CardContent>
    </Card>
  )
}
