import { cn } from '@/utils/shared'

/** A row of pill tabs (specs/076): which of a page's lists is showing. The page keeps the choice in its address. */
export function TabStrip<T extends string>({
  tabs,
  current,
  onChange,
}: {
  tabs: { value: T; label: string }[]
  current: T
  onChange: (value: T) => void
}) {
  return (
    <div role="tablist" className="bg-card ring-border/60 flex flex-wrap gap-1 justify-self-start rounded-3xl p-1 ring-1">
      {tabs.map((tab) => (
        <button
          key={tab.value}
          type="button"
          role="tab"
          aria-selected={tab.value === current}
          onClick={() => onChange(tab.value)}
          className={cn(
            'rounded-full px-3 py-1.5 text-sm font-medium transition-colors',
            tab.value === current ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground',
          )}
        >
          {tab.label}
        </button>
      ))}
    </div>
  )
}
