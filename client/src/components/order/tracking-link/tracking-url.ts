/** The carrier's tracking page for this reference, or null when the carrier has no template (specs/098). */
export function trackingUrl(template: string | null | undefined, reference: string): string | null {
  return template ? template.replace('{reference}', encodeURIComponent(reference)) : null
}
