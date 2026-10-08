import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { PlusIcon, XIcon } from 'lucide-react'
import { Button } from '@ecommerce/ui/button'
import { Input } from '@ecommerce/ui/input'
import { Label } from '@ecommerce/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@ecommerce/ui/select'
import { Textarea } from '@ecommerce/ui/textarea'
import { ApiError } from '@ecommerce/core/config/axios'
import { useCategorySpecificationsIn, useSpecificationChanges } from '@ecommerce/core/hooks/category'
import type { Category, Specification } from '@ecommerce/core/services/category/types'
import { slugOf } from '@ecommerce/core/utils/shared'

type Kind = 'Text' | 'Choice'

/** "Tiếng Việt | English" per line, the English optional. */
export function parseOptionLines(text: string): { code: string; value: string; english: string }[] {
  return text
    .split('\n')
    .map((line) => line.split('|').map((part) => part.trim()))
    .filter(([value]) => !!value)
    .map(([value, english = '']) => ({ code: slugOf(value), value, english }))
}

/**
 * What a category's products are compared by (specs/159, #366), declared here: a text (shown as written) or a choice of
 * options, each in Vietnamese and English. Only the category's own are listed - a department's apply to its categories
 * and are edited on the department. Removing what a product uses is refused by the server, and said in its words.
 */
export function SpecificationsPanel({ category }: { category: Category }) {
  const { t } = useTranslation('admin')
  const vi = useCategorySpecificationsIn(category.id, 'vi')
  const en = useCategorySpecificationsIn(category.id, 'en')
  const changes = useSpecificationChanges(category.id)
  const [name, setName] = useState('')
  const [english, setEnglish] = useState('')
  const [kind, setKind] = useState<Kind>('Choice')
  const [options, setOptions] = useState('')
  const [refusal, setRefusal] = useState<string | null>(null)

  const refused = (error: unknown) => setRefusal(ApiError.from(error).message)
  const own = (vi.data ?? []).filter((s) => s.categoryId === category.id)
  const englishOf = new Map((en.data ?? []).map((s) => [s.id, s]))

  const create = (event: FormEvent) => {
    event.preventDefault()
    setRefusal(null)
    const lines = parseOptionLines(options)
    changes.create
      .mutateAsync({
        code: slugOf(name),
        name: name.trim(),
        kind,
        options: kind === 'Choice' ? lines.map(({ code, value }) => ({ code, value })) : undefined,
        english,
        optionsEnglish: lines.map((l) => l.english),
      })
      .then(() => {
        toast.success(t('categories.specifications.created', { name: name.trim() }))
        setName('')
        setEnglish('')
        setOptions('')
      }, refused)
  }

  return (
    <div className="grid gap-3 border-t pt-3">
      <p className="text-sm font-medium">{t('categories.specifications.title')}</p>
      {own.length === 0 ? (
        <p className="text-muted-foreground text-sm">{t('categories.specifications.none')}</p>
      ) : (
        <ul className="grid gap-2">
          {own.map((specification) => (
            <SpecificationLine
              key={specification.id}
              specification={specification}
              english={englishOf.get(specification.id)}
              changes={changes}
              onRefused={refused}
            />
          ))}
        </ul>
      )}

      <form onSubmit={create} className="grid gap-2 sm:grid-cols-[1fr_1fr_10rem_auto] sm:items-end">
        <div className="grid gap-1.5">
          <Label htmlFor={`${category.id}-spec-name`}>{t('categories.specifications.name')}</Label>
          <Input id={`${category.id}-spec-name`} value={name} onChange={(e) => setName(e.target.value)} required maxLength={100} />
        </div>
        <div className="grid gap-1.5">
          <Label htmlFor={`${category.id}-spec-en`}>{t('categories.specifications.english')}</Label>
          <Input id={`${category.id}-spec-en`} value={english} onChange={(e) => setEnglish(e.target.value)} maxLength={100} />
        </div>
        <div className="grid gap-1.5">
          <Label htmlFor={`${category.id}-spec-kind`}>{t('categories.specifications.kind')}</Label>
          <Select
            items={(['Choice', 'Text'] as const).map((k) => ({ value: k, label: t(`categories.specifications.kinds.${k}`) }))}
            value={kind}
            onValueChange={(value) => setKind((value as Kind) ?? 'Choice')}
          >
            <SelectTrigger id={`${category.id}-spec-kind`} className="h-9! w-full" aria-label={t('categories.specifications.kind')}>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {(['Choice', 'Text'] as const).map((k) => (
                <SelectItem key={k} value={k}>
                  {t(`categories.specifications.kinds.${k}`)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <Button
          type="submit"
          className="rounded-full"
          disabled={!name.trim() || (kind === 'Choice' && parseOptionLines(options).length === 0) || changes.create.isPending}
        >
          <PlusIcon /> {t('categories.specifications.add')}
        </Button>
        {kind === 'Choice' && (
          <div className="grid gap-1.5 sm:col-span-4">
            <Label htmlFor={`${category.id}-spec-options`}>{t('categories.specifications.options')}</Label>
            <Textarea
              id={`${category.id}-spec-options`}
              value={options}
              onChange={(e) => setOptions(e.target.value)}
              placeholder={t('categories.specifications.optionsPlaceholder')}
              rows={3}
            />
          </div>
        )}
      </form>

      {refusal && <p className="text-destructive text-sm">{refusal}</p>}
    </div>
  )
}

function SpecificationLine({
  specification,
  english,
  changes,
  onRefused,
}: {
  specification: Specification
  english: Specification | undefined
  changes: ReturnType<typeof useSpecificationChanges>
  onRefused: (error: unknown) => void
}) {
  const { t } = useTranslation('admin')
  const [option, setOption] = useState('')
  const translated = english?.language === 'en'

  const add = (event: FormEvent) => {
    event.preventDefault()
    const [line] = parseOptionLines(option)
    if (!line) return
    changes.addOption
      .mutateAsync({ specificationId: specification.id, code: line.code, value: line.value, english: line.english })
      .then(() => setOption(''), onRefused)
  }

  return (
    <li className="bg-muted/40 grid gap-2 rounded-2xl p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm">
          <span className="font-medium">{specification.name}</span>
          {translated && <span className="text-muted-foreground"> · {english.name}</span>}
          <span className="text-muted-foreground"> · {t(`categories.specifications.kinds.${specification.kind}`)}</span>
        </p>
        <Button
          variant="ghost"
          size="sm"
          className="rounded-full"
          onClick={() =>
            changes.remove
              .mutateAsync(specification.id)
              .then(() => toast.success(t('categories.specifications.removed', { name: specification.name })), onRefused)
          }
        >
          {t('categories.delete')}
        </Button>
      </div>
      {specification.kind === 'Choice' && (
        <>
          <ul className="flex flex-wrap gap-1.5">
            {specification.options.map((o) => (
              <li key={o.id} className="bg-card flex items-center gap-1 rounded-full py-0.5 pr-1 pl-2.5 text-xs ring-1 ring-black/5">
                {o.value}
                {englishValue(english, o.id) && <span className="text-muted-foreground">/ {englishValue(english, o.id)}</span>}
                <button
                  type="button"
                  aria-label={t('categories.specifications.removeOption', { value: o.value })}
                  className="hover:bg-muted rounded-full p-0.5"
                  onClick={() =>
                    changes.removeOption.mutateAsync({ specificationId: specification.id, optionId: o.id }).catch(onRefused)
                  }
                >
                  <XIcon className="size-3" />
                </button>
              </li>
            ))}
          </ul>
          <form onSubmit={add} className="flex gap-2">
            <Input
              aria-label={t('categories.specifications.newOption', { name: specification.name })}
              placeholder={t('categories.specifications.optionPlaceholder')}
              value={option}
              onChange={(e) => setOption(e.target.value)}
              className="h-8"
            />
            <Button type="submit" size="sm" variant="outline" className="rounded-full" disabled={!option.trim()}>
              <PlusIcon />
            </Button>
          </form>
        </>
      )}
    </li>
  )
}

/** The option's English, when the English read says it is English. */
function englishValue(english: Specification | undefined, optionId: string): string | null {
  const option = english?.options.find((o) => o.id === optionId)
  return option?.language === 'en' ? option.value : null
}
