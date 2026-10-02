import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { PageTitle } from '@ecommerce/core/components/seller/page-title'
import { ErrorMessage, LoadingRows } from '@ecommerce/core/components/query-state'
import { Button } from '@ecommerce/ui/button'
import { Input } from '@ecommerce/ui/input'
import { Label } from '@ecommerce/ui/label'
import { ApiError } from '@ecommerce/core/config/axios'
import { useDeliveryChanges, useDeliverySettings } from '@ecommerce/core/hooks/delivery'
import type { Carrier } from '@ecommerce/core/services/delivery/types'
import { OptionRow } from './option-row'

/**
 * Delivery (specs/098): what each option costs per currency, which are offered and in what order, and the shop's one
 * carrier - its name and tracking page. Checkout reads these from the database, so a change is charged at the next quote;
 * an order already placed keeps what it froze. The server refuses turning off the last option on offer.
 */
export function AdminDeliveryPage() {
  const { t } = useTranslation('admin')
  const settings = useDeliverySettings()
  const changes = useDeliveryChanges()

  return (
    <section className="grid gap-6">
      <PageTitle title={t('deliverySettings.title')} subtitle={t('deliverySettings.subtitle')} />

      {settings.isError ? (
        <ErrorMessage>{t('deliverySettings.loadFailed')}</ErrorMessage>
      ) : settings.isPending ? (
        <LoadingRows />
      ) : (
        <>
          <CarrierForm key={`${settings.data.carrier.name}|${settings.data.carrier.trackingUrlTemplate}`} carrier={settings.data.carrier} changes={changes} />
          <h2 className="text-lg font-semibold">{t('deliverySettings.options')}</h2>
          <ul className="grid gap-3">
            {settings.data.options.map((option) => (
              <OptionRow key={option.code} option={option} changes={changes} />
            ))}
            <OptionRow key="new" option={null} changes={changes} />
          </ul>
        </>
      )}
    </section>
  )
}

function CarrierForm({ carrier, changes }: { carrier: Carrier; changes: ReturnType<typeof useDeliveryChanges> }) {
  const { t } = useTranslation('admin')
  const [name, setName] = useState(carrier.name)
  const [template, setTemplate] = useState(carrier.trackingUrlTemplate ?? '')
  const [refusal, setRefusal] = useState<string | null>(null)

  const save = (event: FormEvent) => {
    event.preventDefault()
    setRefusal(null)
    changes.carrier
      .mutateAsync({ name: name.trim(), trackingUrlTemplate: template.trim() || null })
      .then(() => toast.success(t('deliverySettings.carrierSaved')), (error) => setRefusal(ApiError.from(error).message))
  }

  return (
    <form onSubmit={save} className="bg-card ring-border/60 grid gap-3 rounded-3xl p-4 ring-1 sm:grid-cols-[1fr_2fr_auto] sm:items-end">
      <div className="grid gap-1.5">
        <Label htmlFor="carrier-name">{t('deliverySettings.carrierName')}</Label>
        <Input id="carrier-name" value={name} onChange={(e) => setName(e.target.value)} required maxLength={100} />
      </div>
      <div className="grid gap-1.5">
        <Label htmlFor="carrier-template">{t('deliverySettings.trackingTemplate')}</Label>
        <Input
          id="carrier-template"
          value={template}
          onChange={(e) => setTemplate(e.target.value)}
          placeholder="https://carrier.example/track/{reference}"
          maxLength={500}
        />
      </div>
      <Button type="submit" className="rounded-full" disabled={!name.trim() || changes.carrier.isPending}>
        {t('deliverySettings.save')}
      </Button>
      <p className="text-muted-foreground text-xs sm:col-span-3">{t('deliverySettings.trackingHint')}</p>
      {refusal && <p className="text-destructive text-sm sm:col-span-3">{refusal}</p>}
    </form>
  )
}
