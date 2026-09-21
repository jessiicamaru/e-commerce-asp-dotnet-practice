import { useEffect, useState } from 'react'

// Every service's /health, through the gateway's health routes. The first page this storefront had,
// and still the quickest proof that /api reaches the gateway and the gateway reaches everything.
const services = ['identity', 'catalog', 'order', 'inventory', 'payment', 'cart'] as const

type Result = { status: 'checking' } | { status: 'up' } | { status: 'down'; code: number | string }

export function StatusPage() {
  const [results, setResults] = useState<Record<string, Result>>(
    Object.fromEntries(services.map((s) => [s, { status: 'checking' }])),
  )

  useEffect(() => {
    for (const service of services) {
      fetch(`/api/${service}/health`)
        .then((r) =>
          setResults((prev) => ({ ...prev, [service]: r.ok ? { status: 'up' } : { status: 'down', code: r.status } })),
        )
        .catch((e: unknown) =>
          setResults((prev) => ({ ...prev, [service]: { status: 'down', code: String(e) } })),
        )
    }
  }, [])

  return (
    <section>
      <h1>Backend status</h1>
      <p className="muted">Each service's health, checked through the gateway.</p>
      <table className="status">
        <tbody>
          {services.map((service) => {
            const r = results[service]
            return (
              <tr key={service}>
                <td>{service}</td>
                <td className={r.status}>
                  {r.status === 'checking' ? 'checking…' : r.status === 'up' ? 'up' : `down (${r.code})`}
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
      <p className="muted">
        The orchestrator has no health endpoint - it has no HTTP surface at all - so it cannot be listed here.
      </p>
    </section>
  )
}
