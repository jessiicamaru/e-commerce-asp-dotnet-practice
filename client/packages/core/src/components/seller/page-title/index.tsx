/** The heading of a seller page: a title, what the page is for, and room on the right for its actions. */
export function PageTitle({ title, subtitle, children }: { title: string; subtitle?: string; children?: React.ReactNode }) {
  return (
    <header className="flex flex-wrap items-end justify-between gap-3">
      <div className="grid gap-1">
        <h1 className="text-2xl font-bold tracking-tight">{title}</h1>
        {subtitle && <p className="text-muted-foreground max-w-prose text-sm">{subtitle}</p>}
      </div>
      {children}
    </header>
  )
}
