# Research: Which shop each parcel comes from

> Completed on 2026-09-27, after the feature merged (#80), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md. Decisions as written before the code; labels added so each
> reads like specs/001.

**Feature**: [spec.md](spec.md)

## D1 - Frozen onto the line, like the seller id

**Decision**: `order_items.SellerName`, copied at checkout, never written again.

**Rationale**: The order already freezes who sold each line (specs/034 D1) because it is a record of a purchase.
The name belongs with it: the customer bought from "Mai Lens Hà Nội", and a rename next month should
not make an old order say they bought from somewhere else. It also keeps the read inside Order's
database - an order page makes no call to Catalog, and works with Catalog down.

**Alternatives considered**: looking the name up at read time through Catalog - rejected. It would show today's name on a past
purchase, and add a synchronous dependency to a page that has none.

## D2 - It rides the pricing answer, like the seller id

**Decision**: `PricedVariant` gains `optional string seller_name = 10`, filled from Catalog's `sellers`
read model in the same `PriceVariants` call checkout already makes - **one batched lookup** per
request (`ISellerRepository.GetNamesAsync`), not one per line.

**Rationale**: Set only when the product has a seller **and** the read model knows their name. The shop's own goods
and an unknown name are both left unset. Unlike `seller_id` (specs/034 D2) nothing needs to tell
"unknown" from "the shop's" here: the seller id already says whose it is, and a missing name is a
missing label, not a misattributed sale. Order therefore records `null` for both and logs nothing.

As built, Order's `GrpcCatalogPrices.SellerNameOf` also treats a present-but-blank name as null, because
it would print as a blank.

**Alternatives considered**: a lookup per line - rejected (the rule Catalog's `ShopNamesAsync` cites
from specs/027: a page of lines must not become a lookup per line). No other alternative is recorded.

## D3 - The shop's own parcel is labelled by the client

**Decision**: the server sends `IsShop`; the client words it.

**Rationale**: A part with no seller is the shop's. `ShipmentResponse` gains `SellerName` and `IsShop`; the client
words the shop's parcel ("Cửa hàng" / "The shop") in the reader's language, which a name frozen on
the server could not do. A seller's part with no recorded name shows as it did before this feature.

As built, a parcel's `SellerName` is the first non-null name frozen on its lines (every line of one part
is the same seller's).

**Alternatives considered**: a name for the shop frozen on the server - rejected above, it could not
follow the reader's language.

## D4 - No backfill

**Decision**: older orders keep no name.

**Rationale**: Same reason as specs/034 D7: the only source is Catalog's database, and asking it now would answer
with today's name - which D1 exists to refuse.

**Alternatives considered**: backfilling from Catalog - rejected above.
