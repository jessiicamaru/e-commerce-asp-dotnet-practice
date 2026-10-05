// TEMPORARY - specs/150's negative control for CodeQL: a deliberate DOM XSS. Reverted before merge.
export function negativeControl() {
  const message = new URLSearchParams(window.location.search).get('message') ?? ''
  document.body.innerHTML = message
}
