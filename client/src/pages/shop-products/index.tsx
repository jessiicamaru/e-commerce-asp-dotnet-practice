import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { EllipsisIcon, ExternalLinkIcon, PencilIcon, SearchIcon } from 'lucide-react'
import { ProductImage } from '@/components/product/product-image'
import { PageTitle } from '@/components/seller/page-title'
import { Pager } from '@/components/shared/pager'
import { Price } from '@/components/shared/price'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { InputGroup, InputGroupAddon, InputGroupInput } from '@/components/ui/input-group'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { PAGE_SIZE } from '@/constants/shared'
import { useAuth } from '@/context/auth/useAuth'
import { useMyProducts } from '@/hooks/product'
import { useListingStock } from '@/hooks/stock'
import { StockBadge } from '@/components/product/stock-badge'

/**
 * A seller's listings as a table they can manage from (specs/028): what each one is, what it costs,
 * **how many are actually left**, and a way into it.
 *
 * <p>
 * The stock column is Inventory's number, summed over the listing's variants - not Catalog's
 * "in stock / out of stock", which is a read model a few seconds behind and never a count (specs/004).
 * A seller deciding what to reorder needs the number.
 * </p>
 */
export function ShopProductsPage() {
  const { t } = useTranslation('seller')
  const { isSeller } = useAuth()
  const navigate = useNavigate()
  const [params, setParams] = useSearchParams()
  const page = Number(params.get('page') ?? '1') || 1
  const searchTerm = params.get('q') ?? ''
  const [draft, setDraft] = useState(searchTerm)

  const listings = useMyProducts({ pageNumber: page, pageSize: PAGE_SIZE, searchTerm }, isSeller)
  const stock = useListingStock((listings.data?.items ?? []).map((product) => product.id))

  return (
    <section className="grid gap-6">
      <PageTitle title={t('menu.products')} subtitle={t('subtitle')} />

      <form
        className="flex gap-2"
        onSubmit={(event) => {
          event.preventDefault()
          setParams(draft.trim() ? { q: draft.trim() } : {})
        }}
      >
        <InputGroup className="bg-card h-10 max-w-md rounded-full">
          <InputGroupAddon>
            <SearchIcon />
          </InputGroupAddon>
          <InputGroupInput
            type="search"
            value={draft}
            placeholder={t('products.search')}
            aria-label={t('products.search')}
            onChange={(event) => setDraft(event.target.value)}
          />
        </InputGroup>
      </form>

      {listings.isError ? (
        <ErrorMessage>{t('listing.loadFailed')}</ErrorMessage>
      ) : listings.isPending || !listings.data ? (
        <LoadingRows rows={5} />
      ) : listings.data.totalCount === 0 ? (
        <p className="bg-card ring-border/60 text-muted-foreground rounded-3xl p-8 text-sm ring-1">
          {searchTerm ? t('products.noMatch', { term: searchTerm }) : t('empty.body')}
        </p>
      ) : (
        <>
          <div className="bg-card ring-border/60 overflow-hidden rounded-3xl ring-1">
            <Table>
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead className="pl-5">{t('products.columns.product')}</TableHead>
                  <TableHead className="hidden md:table-cell">{t('products.columns.variants')}</TableHead>
                  <TableHead>{t('products.columns.price')}</TableHead>
                  <TableHead>{t('products.columns.stock')}</TableHead>
                  <TableHead className="w-12 pr-5" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {listings.data.items.map((product) => {
                  const counted = stock.byProduct[product.id]

                  return (
                    <TableRow
                      key={product.id}
                      className="cursor-pointer"
                      onClick={() => navigate(`/shop/products/${product.id}`)}
                    >
                      <TableCell className="py-3 pl-5">
                        <div className="flex items-center gap-3">
                          <span className="w-12 shrink-0">
                            <ProductImage product={product} thumb />
                          </span>
                          <span className="grid min-w-0">
                            <Link
                              to={`/shop/products/${product.id}`}
                              className="truncate font-medium hover:underline"
                              onClick={(event) => event.stopPropagation()}
                            >
                              {product.name}
                            </Link>
                            <span className="text-muted-foreground text-xs">{product.sku}</span>
                          </span>
                        </div>
                      </TableCell>
                      <TableCell className="text-muted-foreground hidden md:table-cell">
                        {t('products.variantCount', { count: product.variantCount })}
                      </TableCell>
                      <TableCell>
                        {product.priceVaries && (
                          <span className="text-muted-foreground mr-1 text-xs">{t('products.from')}</span>
                        )}
                        <Price value={product.price} currency={product.currency} className="font-medium" />
                      </TableCell>
                      <TableCell>
                        <StockBadge
                          available={counted?.available}
                          reserved={counted?.reserved ?? 0}
                          pending={stock.isPending && counted?.known === 0}
                        />
                      </TableCell>
                      <TableCell className="pr-5" onClick={(event) => event.stopPropagation()}>
                        <DropdownMenu>
                          <DropdownMenuTrigger
                            render={<Button variant="ghost" size="icon-sm" className="rounded-full" />}
                            aria-label={t('products.actions', { name: product.name })}
                          >
                            <EllipsisIcon />
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end" className="w-52">
                            <DropdownMenuItem onClick={() => navigate(`/shop/products/${product.id}`)}>
                              <PencilIcon /> {t('products.edit')}
                            </DropdownMenuItem>
                            <DropdownMenuItem onClick={() => navigate(`/products/${product.id}`)}>
                              <ExternalLinkIcon /> {t('edit.view')}
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    </TableRow>
                  )
                })}
              </TableBody>
            </Table>
          </div>

          <Pager
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={listings.data.totalCount}
            onChange={(next) => setParams({ ...(searchTerm ? { q: searchTerm } : {}), page: String(next) })}
          />
        </>
      )}

    </section>
  )
}
