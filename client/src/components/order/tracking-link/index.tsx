import { useCarrier } from '@/hooks/delivery'
import { trackingUrl } from './tracking-url'

/**
 * A parcel's tracking reference - a link to the carrier's page when the shop has set one, the reference as text
 * otherwise. One place, so every page that shows a reference links it the same way.
 */
export function TrackingLink({ reference }: { reference: string }) {
  const carrier = useCarrier()
  const url = trackingUrl(carrier.data?.trackingUrlTemplate, reference)

  if (!url) return <span className="font-mono font-semibold">{reference}</span>

  return (
    <a href={url} target="_blank" rel="noreferrer noopener" className="font-mono font-semibold underline underline-offset-2">
      {reference}
      {carrier.data?.name && <span className="text-muted-foreground font-sans font-normal no-underline"> ({carrier.data.name})</span>}
    </a>
  )
}
