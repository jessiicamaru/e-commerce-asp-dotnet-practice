import { useState } from 'react'
import { cn } from '@/utils/shared'
import type { Product } from '@/services/product/types'

/**
 * The product's picture, or a lens tile when it has none (specs/019) - and when the picture fails to
 * load, so a broken image never reaches a shopper. Served by Catalog through the gateway.
 *
 * **The tile is meant to look deliberate, not broken.** The catalogue has no photographs yet, and
 * there is no honest way to obtain them here, so the fallback says "no photograph" in the shop's own
 * visual language rather than showing a grey box with a letter in it. The tint is derived from the
 * product's id, so a given camera keeps the same tile everywhere it appears and a grid of them reads
 * as variety rather than as repetition.
 */
export function ProductImage({
  product,
  imageUrl,
  large = false,
}: {
  product: Product
  /**
   * Shown instead of the product's — the chosen variant's picture (specs/032). Undefined means
   * "use the product's"; the server has already folded the fallback into it, so passing
   * `variant.imageUrl` is always correct when a variant is chosen.
   */
  imageUrl?: string | null
  large?: boolean
}) {
  const [failed, setFailed] = useState<string | null>(null)
  const shown = imageUrl === undefined ? product.imageUrl : imageUrl
  const missing = !shown || failed === shown

  const shape = cn(
    'w-full overflow-hidden rounded-2xl',
    large ? 'aspect-square' : 'aspect-[4/3]',
  )

  if (missing) {
    // A hue from the id: stable per product, spread across the wheel, and kept pale so the tile
    // never competes with the price sitting under it.
    const hue = [...product.id].reduce((total, character) => total + character.charCodeAt(0), 0) % 360

    return (
      <div
        className={cn(shape, 'relative grid place-items-center')}
        style={{
          background: `linear-gradient(145deg,
            oklch(0.955 0.035 ${hue}) 0%,
            oklch(0.93 0.05 ${(hue + 40) % 360}) 100%)`,
        }}
        aria-hidden="true"
      >
        <Lens className={cn('opacity-25', large ? 'size-40' : 'size-20')} hue={hue} />
      </div>
    )
  }

  return (
    <img
      className={cn(shape, 'bg-card object-contain')}
      src={shown!}
      alt={product.name}
      loading="lazy"
      onError={() => setFailed(shown)}
    />
  )
}

/** An aperture, drawn rather than imported: six blades, which is what a camera actually has. */
function Lens({ className, hue }: { className?: string; hue: number }) {
  const stroke = `oklch(0.42 0.06 ${hue})`

  return (
    <svg viewBox="0 0 48 48" fill="none" className={className} stroke={stroke} strokeWidth={1.5}>
      <circle cx="24" cy="24" r="19" />
      <circle cx="24" cy="24" r="8.5" />
      {[0, 60, 120, 180, 240, 300].map((angle) => (
        <line
          key={angle}
          x1="24"
          y1="24"
          x2={24 + 19 * Math.cos((angle * Math.PI) / 180)}
          y2={24 + 19 * Math.sin((angle * Math.PI) / 180)}
          strokeWidth={1}
        />
      ))}
    </svg>
  )
}
