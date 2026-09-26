import type { ReactNode } from 'react'

/** One titled card of an insights page (specs/047, 068). */
export function InsightPanel({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="bg-card ring-border/60 grid content-start gap-4 rounded-3xl p-5 ring-1">
      <h2 className="font-semibold">{title}</h2>
      {children}
    </div>
  )
}
