import { Button } from '@/components/ui/button'

export function Pager({
  page,
  totalPages,
  onChange,
  previousLabel = 'Previous',
  nextLabel = 'Next',
}: {
  page: number
  totalPages: number
  onChange: (page: number) => void
  previousLabel?: string
  nextLabel?: string
}) {
  if (totalPages <= 1) {
    return null
  }

  return (
    <nav className="mt-6 flex items-center justify-center gap-4">
      <Button variant="outline" disabled={page <= 1} onClick={() => onChange(page - 1)}>
        {previousLabel}
      </Button>
      <span className="text-muted-foreground text-sm">
        Page {page} of {totalPages}
      </span>
      <Button variant="outline" disabled={page >= totalPages} onClick={() => onChange(page + 1)}>
        {nextLabel}
      </Button>
    </nav>
  )
}
