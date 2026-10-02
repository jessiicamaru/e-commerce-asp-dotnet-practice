import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { SaveIcon, Trash2Icon } from 'lucide-react'
import { toast } from 'sonner'
import { ServerError } from '@/components/shared/server-error'
import { Badge } from '@ecommerce/ui/badge'
import { Button } from '@ecommerce/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@ecommerce/ui/card'
import { Input } from '@ecommerce/ui/input'
import { Label } from '@ecommerce/ui/label'
import { Separator } from '@ecommerce/ui/separator'
import { Textarea } from '@ecommerce/ui/textarea'
import { LANGUAGES } from '@ecommerce/core/config/i18n'
import { useSetProductTranslation } from '@ecommerce/core/hooks/product'
import type { Product, ProductTranslationText } from '@ecommerce/core/services/product/types'

/**
 * The product's own text in each of the shop's languages (specs/021, specs/124 #240). A language without one shows
 * its readers the original; saving gives it one, removing takes it away again.
 *
 * <p>
 * Every language is offered, the default one too: the original is whatever the seller typed when listing, in
 * whatever language they were browsing in, so it is not necessarily Vietnamese. The text shown is as stored - from
 * `translations` on the lookup, never the response's per-field fallback.
 * </p>
 */
export function ProductTranslationsCard({ product }: { product: Product }) {
  const { t } = useTranslation('seller')

  return (
    <Card className="rounded-3xl">
      <CardHeader>
        <CardTitle>{t('text.title')}</CardTitle>
        <CardDescription>{t('text.hint')}</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-6">
        {LANGUAGES.map((language, index) => (
          <div key={language} className="grid gap-6">
            {index > 0 && <Separator />}
            <LanguageText
              productId={product.id}
              language={language}
              stored={product.translations?.find((text) => text.language === language) ?? null}
            />
          </div>
        ))}
      </CardContent>
    </Card>
  )
}

function LanguageText({
  productId,
  language,
  stored,
}: {
  productId: string
  language: string
  stored: ProductTranslationText | null
}) {
  const { t } = useTranslation('seller')
  const write = useSetProductTranslation(productId)
  const [name, setName] = useState(stored?.name ?? '')
  const [description, setDescription] = useState(stored?.description ?? '')
  const languageName = t(`language.${language}`, { ns: 'common' })

  const changed = name.trim() !== (stored?.name ?? '') || (description.trim() || null) !== (stored?.description ?? null)

  function save(event: React.FormEvent) {
    event.preventDefault()
    // mutateAsync: the card is keyed by the stored text and replaced by the save (see ProductDetailsCard).
    write
      .mutateAsync({ language, text: { name: name.trim(), description: description.trim() || null } })
      .then(() => toast.success(t('text.saved', { language: languageName })))
      .catch(() => {})
  }

  function remove() {
    write
      .mutateAsync({ language, text: null })
      .then(() => toast.success(t('text.removed', { language: languageName })))
      .catch(() => {})
  }

  return (
    <form onSubmit={save} className="grid gap-3" aria-label={languageName}>
      <div className="flex flex-wrap items-center gap-2">
        <h3 className="font-semibold">{languageName}</h3>
        {stored ? (
          <Badge variant="secondary">{t('text.own')}</Badge>
        ) : (
          <span className="text-muted-foreground text-xs">{t('text.none', { language: languageName })}</span>
        )}
      </div>
      <div className="grid gap-2">
        <Label htmlFor={`text-name-${language}`}>{t('create.name')}</Label>
        <Input
          id={`text-name-${language}`}
          maxLength={200}
          className="h-10 rounded-xl"
          value={name}
          onChange={(event) => setName(event.target.value)}
        />
      </div>
      <div className="grid gap-2">
        <Label htmlFor={`text-description-${language}`}>{t('create.description')}</Label>
        <Textarea
          id={`text-description-${language}`}
          rows={3}
          maxLength={2000}
          className="rounded-xl"
          value={description}
          onChange={(event) => setDescription(event.target.value)}
        />
      </div>
      <ServerError error={write.error} fallback={t('listing.loadFailed')} />
      <div className="flex flex-wrap gap-2">
        <Button type="submit" className="rounded-full" disabled={!changed || !name.trim() || write.isPending}>
          <SaveIcon /> {t('text.save', { language: languageName })}
        </Button>
        {stored && (
          <Button type="button" variant="outline" className="rounded-full" disabled={write.isPending} onClick={remove}>
            <Trash2Icon /> {t('text.remove', { language: languageName })}
          </Button>
        )}
      </div>
    </form>
  )
}
