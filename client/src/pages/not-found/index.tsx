import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { SearchIcon } from 'lucide-react'
import { Button, buttonVariants } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

/**
 * Where an address no route knows lands (specs/120, #243): what happened, a search box and the way back to the shop -
 * in the reader's language. It was "Not found." in English at the top left, a dead end for somebody who was looking
 * for something. A known page's own 404 - an order, a product, a shop - keeps its own message.
 */
export function NotFoundPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [term, setTerm] = useState('')

  function search(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const query = term.trim()
    navigate(query ? `/?q=${encodeURIComponent(query)}` : '/')
  }

  return (
    <section className="mx-auto grid max-w-lg gap-5 py-12 text-center">
      <p className="text-muted-foreground font-mono text-sm">404</p>
      <h1 className="text-3xl font-bold tracking-tight">{t('notFound.title')}</h1>
      <p className="text-muted-foreground">{t('notFound.body')}</p>
      <form onSubmit={search} role="search" className="flex gap-2">
        <Input
          type="search"
          value={term}
          onChange={(event) => setTerm(event.target.value)}
          placeholder={t('notFound.searchPlaceholder')}
          aria-label={t('notFound.searchPlaceholder')}
          className="rounded-full"
        />
        <Button type="submit" className="rounded-full">
          <SearchIcon /> {t('notFound.search')}
        </Button>
      </form>
      <Link to="/" className={buttonVariants({ variant: 'outline', className: 'mx-auto rounded-full' })}>
        {t('notFound.home')}
      </Link>
    </section>
  )
}
