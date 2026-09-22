import { useTranslation } from 'react-i18next'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import type { Address } from '@/services/address/types'
import { describeAddress } from '@/utils/address'

export function AddressCard({
  address,
  onEdit,
  onMakeDefault,
  onDelete,
}: {
  address: Address
  onEdit: () => void
  onMakeDefault: () => void
  onDelete: () => void
}) {
  const { t } = useTranslation('auth')

  return (
    <Card>
      <CardContent className="flex flex-col gap-1 p-4">
        <span className="flex items-center gap-2 font-medium">
          {address.recipientName}
          {address.isDefault && <Badge>{t('addresses.default')}</Badge>}
        </span>
        <span className="text-sm">{describeAddress(address)}</span>
        {address.phone && <span className="text-muted-foreground text-xs">{address.phone}</span>}
        <div className="mt-2 flex flex-wrap gap-2">
          <Button variant="outline" size="sm" onClick={onEdit}>
            {t('action.edit', { ns: 'common' })}
          </Button>
          {!address.isDefault && (
            <Button variant="outline" size="sm" onClick={onMakeDefault}>
              {t('addresses.makeDefault')}
            </Button>
          )}
          <Button variant="ghost" size="sm" onClick={onDelete}>
            {t('action.delete', { ns: 'common' })}
          </Button>
        </div>
      </CardContent>
    </Card>
  )
}
