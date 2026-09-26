# Data Model: A Cart and an Address Book

> Written on 2026-09-27, after the feature merged (#47), from the code at that merge, the pull request
> and docs/features/shopping-and-checkout.md and docs/architecture/storefront.md.

**Feature**: [spec.md](./spec.md)

**No table changed and no migration was added.** The pull request touched no server file. The data the
pages show lives in Cart (`carts`, `cart_lines` - specs/010) and Identity (`delivery_addresses` -
specs/011), unchanged.

## Client types

`client/src/api/cart.ts`:

| Type | Fields |
| :--- | :--- |
| `Cart` | `lines: CartLine[]`, `estimatedTotal: number \| null`, `canCheckOut: boolean`, `pricesAvailable: boolean` |
| `CartLine` | `productId`, `name \| null`, `quantity`, `unitPrice \| null`, `lineTotal \| null`, `status` |
| `CartLineStatus` | `'Available' \| 'NotForSale' \| 'NoLongerAvailable' \| 'PriceUnavailable'` (widened to `string`) |

`client/src/api/addresses.ts`:

| Type | Fields |
| :--- | :--- |
| `AddressFields` | `recipientName`, `line1`, `line2 \| null`, `city`, `region \| null`, `postalCode`, `country` (ISO 3166 alpha-2; decides the tax rate at checkout), `phone \| null` |
| `Address` | `AddressFields` + `id`, `isDefault` |

These mirror `CartResponse` / `CartLineResponse` (Cart) and the address response (Identity) at the
merge. Nothing is stored in the browser.
