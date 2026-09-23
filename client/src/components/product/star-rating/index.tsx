import { StarIcon } from 'lucide-react'
import { cn } from '@/utils/shared'

/**
 * Five stars filled to a rating (specs/046), with the number in words for a screen reader. Whole and
 * half stars only - an average of 4.3 draws four and a half, and says 4.3.
 */
export function StarRating({ value, label, className }: { value: number; label: string; className?: string }) {
  const rounded = Math.round(value * 2) / 2

  return (
    <span role="img" aria-label={label} className={cn('inline-flex items-center gap-0.5', className)}>
      {[1, 2, 3, 4, 5].map((star) => (
        <span key={star} className="relative inline-block size-4">
          <StarIcon aria-hidden className="text-muted-foreground/40 absolute inset-0 size-4" />
          {rounded >= star - 0.5 && (
            <span className="absolute inset-0 overflow-hidden" style={{ width: rounded >= star ? '100%' : '50%' }}>
              <StarIcon aria-hidden className="size-4 fill-amber-400 text-amber-400" />
            </span>
          )}
        </span>
      ))}
    </span>
  )
}

/** Choosing 1-5 stars: five toggle buttons, so each says which it is and which is pressed. */
export function StarInput({ value, onChange, labelFor }: { value: number; onChange: (value: number) => void; labelFor: (n: number) => string }) {
  return (
    <span className="inline-flex gap-1">
      {[1, 2, 3, 4, 5].map((star) => (
        <button
          key={star}
          type="button"
          aria-label={labelFor(star)}
          aria-pressed={star === value}
          onClick={() => onChange(star)}
          className="rounded-md p-0.5 outline-none focus-visible:ring-3"
        >
          <StarIcon className={cn('size-7', star <= value ? 'fill-amber-400 text-amber-400' : 'text-muted-foreground/50')} />
        </button>
      ))}
    </span>
  )
}
