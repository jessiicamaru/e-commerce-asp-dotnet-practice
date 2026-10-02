import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { currentApp, storefrontUrl } from '@ecommerce/core/config/apps'

/**
 * A link to a product's page (specs/137). In the storefront it is an ordinary in-app link; in the back office the
 * product's page belongs to another application, so it is the storefront's address, in a new tab - leaving the console
 * where it was.
 */
export function ProductLink({ productId, className, children }: { productId: string; className?: string; children: ReactNode }) {
  if (currentApp() === 'storefront') {
    return (
      <Link to={`/products/${productId}`} className={className}>
        {children}
      </Link>
    )
  }
  return (
    <a href={storefrontUrl(`/products/${productId}`)} target="_blank" rel="noreferrer" className={className}>
      {children}
    </a>
  )
}
