# Research: A shop's rating

## D1 - One computation, shared with the seller's insights

**Decision**: the shop's rating is each product's stored average (`products.RatingAverage`) weighted by its count
(`RatingCount`), over every product the seller has - the computation the seller's insights already made (specs/068,
research D3) - moved into one place both reads call.

**Rationale**: a seller reading 4.6 on their insights page and shoppers reading 4.4 on their shop page would be two
truths about one shop. Each product's average is recomputed from its visible reviews on every write, hide and restore
(specs/046), so the weighted sum is the mean of the visible reviews, give or take the two-decimal rounding of each
product's average.

**Rejected**:

- **Averaging `product_reviews` directly**: exact to the last digit, but a second definition next to the insights' one,
  and a join over every review a shop ever received for a number already summed per product.
- **A stored `sellers.RatingAverage`, updated on each review**: one more write on every review path and a value that can
  drift from its source. The read is one aggregate over a shop's products, and the page is in the read cache.

## D2 - Off-shelf products count

**Decision**: every product the seller has, whatever its state.

**Rationale**: a product's reviews are the shop's history. Counting only what is on the shelf would let a seller withdraw
a badly reviewed product and see the shop's rating rise. Staff alone delete products (specs/024), and a deleted product
takes its reviews with it.

**Rejected**: only products on the shelf - matches what the page lists, at the price above.

## D3 - The product page reads the shop page

**Decision**: the product page asks `GET /api/shops/{sellerId}` - the same query and cache key as the shop page - for a
seller's product, and shows the rating beside "Sold by". The listing's cards do not.

**Rationale**: one small cached read per product page. The product response could carry the shop's rating, but every
product read (the listing included) would then pay for an aggregate a card does not show.

**Rejected**: the rating on each listing card - a twelfth of a page's requests each, for a number that reads the same on
every card of one shop.

## D4 - The cache

**Decision**: nothing new. The shop page is already a cached read (specs/157), and every review write, hide and restore
updates `products`, which `CatalogueWrites` evicts on.

## Known limit

`products.SellerId` has no index. The shop page already counted a shop's products with the same filter, and the listing's
`sellerId` filters on it too; both are answered from the read cache after the first time. An index is a change of its own
when a measured catalogue asks for it.
