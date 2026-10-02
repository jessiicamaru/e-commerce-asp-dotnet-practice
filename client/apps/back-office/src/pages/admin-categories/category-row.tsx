import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { Button } from '@ecommerce/ui/button'
import { Input } from '@ecommerce/ui/input'
import { Label } from '@ecommerce/ui/label'
import { ApiError } from '@ecommerce/core/config/axios'
import type { useCategoryChanges } from '@ecommerce/core/hooks/category'
import type { Category } from '@ecommerce/core/services/category/types'

type Changes = ReturnType<typeof useCategoryChanges>

/**
 * One category: its Vietnamese and English names at a glance, and - opened - both edited together. English counts as
 * translated only when the server said the English list's name is in English (`language === 'en'`); otherwise the field
 * starts empty, and saving it empty removes the translation.
 */
export function CategoryRow({ vi, en, changes }: { vi: Category; en: Category | undefined; changes: Changes }) {
  const { t } = useTranslation('admin')
  const translated = en?.language === 'en'
  const [open, setOpen] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [viName, setViName] = useState(vi.name)
  const [viDescription, setViDescription] = useState(vi.description ?? '')
  const [enName, setEnName] = useState(translated ? en.name : '')
  const [enDescription, setEnDescription] = useState(translated ? (en.description ?? '') : '')
  const [refusal, setRefusal] = useState<string | null>(null)

  const refused = (error: unknown) => setRefusal(ApiError.from(error).message)

  const save = (event: FormEvent) => {
    event.preventDefault()
    setRefusal(null)
    changes.save
      .mutateAsync({
        id: vi.id,
        vi: { name: viName.trim(), description: viDescription.trim() || null },
        en: { name: enName.trim(), description: enDescription.trim() || null },
      })
      .then(() => {
        toast.success(t('categories.saved', { name: viName.trim() }))
        setOpen(false)
      }, refused)
  }

  const remove = () => {
    setRefusal(null)
    changes.remove.mutateAsync(vi.id).then(() => toast.success(t('categories.deleted', { name: vi.name })), refused)
    setConfirming(false)
  }

  return (
    <li className="bg-card ring-border/60 grid gap-3 rounded-3xl p-4 ring-1">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="min-w-0">
          <p className="font-medium">{vi.name}</p>
          <p className="text-muted-foreground text-sm">
            {translated ? en.name : t('categories.noEnglish')} · <span className="font-mono text-xs">/{vi.slug}</span>
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" className="rounded-full" onClick={() => setOpen((o) => !o)}>
            {t('categories.edit')}
          </Button>
          {confirming ? (
            <Button variant="destructive" size="sm" className="rounded-full" onClick={remove}>
              {t('categories.confirmDelete')}
            </Button>
          ) : (
            <Button variant="ghost" size="sm" className="rounded-full" onClick={() => setConfirming(true)}>
              {t('categories.delete')}
            </Button>
          )}
        </div>
      </div>

      {open && (
        <form onSubmit={save} className="grid gap-3 sm:grid-cols-2">
          <fieldset className="grid gap-2">
            <legend className="text-sm font-medium">{t('categories.vietnamese')}</legend>
            <Label htmlFor={`${vi.id}-vi-name`}>{t('categories.name')}</Label>
            <Input id={`${vi.id}-vi-name`} value={viName} onChange={(e) => setViName(e.target.value)} required maxLength={100} />
            <Label htmlFor={`${vi.id}-vi-description`}>{t('categories.description')}</Label>
            <Input id={`${vi.id}-vi-description`} value={viDescription} onChange={(e) => setViDescription(e.target.value)} maxLength={500} />
          </fieldset>
          <fieldset className="grid gap-2">
            <legend className="text-sm font-medium">{t('categories.english')}</legend>
            <Label htmlFor={`${vi.id}-en-name`}>{t('categories.name')}</Label>
            <Input id={`${vi.id}-en-name`} value={enName} onChange={(e) => setEnName(e.target.value)} maxLength={100} />
            <Label htmlFor={`${vi.id}-en-description`}>{t('categories.description')}</Label>
            <Input id={`${vi.id}-en-description`} value={enDescription} onChange={(e) => setEnDescription(e.target.value)} maxLength={500} />
          </fieldset>
          <Button type="submit" className="justify-self-start rounded-full" disabled={!viName.trim() || changes.save.isPending}>
            {t('categories.save')}
          </Button>
        </form>
      )}

      {refusal && <p className="text-destructive text-sm">{refusal}</p>}
    </li>
  )
}
