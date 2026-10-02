/**
 * Which page numbers to draw: always the first and the last, the current one with a neighbour on
 * each side, and a gap (`'…'`) wherever numbers are skipped.
 *
 * `pageWindow(6, 12)` → `[1, '…', 5, 6, 7, '…', 12]`. A gap is only drawn where it hides at least two
 * pages - hiding one page behind "…" takes the same room as just showing it.
 */
export function pageWindow(page: number, totalPages: number): (number | '…')[] {
  if (totalPages <= 7) {
    return Array.from({ length: totalPages }, (_, index) => index + 1)
  }

  const wanted = new Set([1, totalPages, page - 1, page, page + 1])
  // Near either end, keep five in a row so the bar does not change width as you walk through it.
  if (page <= 4) [2, 3, 4, 5].forEach((n) => wanted.add(n))
  if (page >= totalPages - 3) [totalPages - 4, totalPages - 3, totalPages - 2, totalPages - 1].forEach((n) => wanted.add(n))

  const pages = [...wanted].filter((n) => n >= 1 && n <= totalPages).sort((a, b) => a - b)
  const out: (number | '…')[] = []

  pages.forEach((n, index) => {
    const previous = pages[index - 1]
    if (previous !== undefined && n - previous === 2) out.push(previous + 1)
    else if (previous !== undefined && n - previous > 2) out.push('…')
    out.push(n)
  })

  return out
}
