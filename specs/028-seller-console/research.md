# Research: A seller can actually sell

## D1 - How does the storefront learn that somebody is a seller?

**Decision**: Identity returns the roles on the authentication response, and the storefront keeps
them beside the user in the auth context.

**Rejected - decode the JWT in the browser.** The token is already in memory and the roles are in
it, so this needs no backend change at all. It was rejected for what it teaches rather than for
what it costs: a decoded token in client code looks authoritative, and the next person to need a
rule writes it against the decode. The claim set is also ours to change - `MapInboundClaims = false`
and `RoleClaimType = "role"` are load-bearing settings documented as easy to break, and a browser
parser would become a third place that depends on them.

**Rejected - probe `GET /api/sellers/me`.** Works today with no change whatsoever: a seller gets
200, anybody else gets 403. But it spends a request on every page load to answer a question the
sign-in already answered, and it makes a 403 - a refusal - part of the normal path, which is the
kind of thing that later gets logged as an error and investigated.

The addition is additive and touches no token: `AuthResponse` gains `Roles`, a `string[]`. Sign-in,
sign-up, register-seller and refresh all return it, because a reload must restore the same answer
as a sign-in or the shop disappears when the page is refreshed.

**What this does NOT become**: a permission. The storefront uses roles to decide what to *show*.
Every write is still refused or allowed by the server, by `SellerOwnership` and the controller
attributes. A seller who edits their own JavaScript gets a menu, not a product.

## D2 - Create a product: one form or a wizard?

**Decision**: one form, one submission, one product with one variant.

`POST /api/products` already creates the first variant and reuses the product id for it
(specs/020), so the smallest honest create is: name, category, description, SKU, price. A wizard
would be truer to the data model - product, then variants, then prices per currency, then
translations, then an image - and it would also mean a seller cannot finish listing anything
without completing five steps. The form creates something sellable; US3 corrects it afterwards.

**Consequence recorded**: the product is created with text in ONE language and a price in ONE
currency. It therefore falls back to that language for the other (specs/021 per-field fallback) and
has **no price at all** in the other currency, so it comes back `sellable: false` there (specs/022,
and deliberately so). The form says this rather than hiding it.

**And the currency is NOT the one the seller is browsing in.** `CreateProductCommand.Price` sets
`product_variants.Price`, which specs/022 defines as the *default* currency's amount, and its
validator enforces exactly that - `MustFitTheCurrency(money.Value, _ => money.Value.DefaultCurrency)`.
A seller reading the shop in USD who types 1999 into a field labelled with the active currency would
be listing a 1,999-dong camera and would be told nothing. The field is therefore labelled with the
**default** currency, whatever the seller is browsing in, and the second price is set afterwards
through the per-currency endpoint. Checked in the handler and the validator, not assumed from the
field name.

## D3 - Where do the seller pages live?

**Decision**: inside `MainLayout`, at `/shop`, `/shop/products/new`, `/shop/products/:id`.

A seller is a customer too (specs/027 grants both roles), holds one session and one cart. A
separate layout would duplicate the top bar, the language switcher and the currency switcher to
produce a page that differs only by its navigation.

## D4 - What does a refusal look like?

**Decision**: the server's own message, verbatim, in the form.

The whole reason `GlobalExceptionHandler` shows `ValidationException`, `ConflictException` and
`NotFoundException` details outside Development (found in specs/022 through a Bruno test) is so
that a caller can tell the person what went wrong. "9.99 is not a price in VND" and "SKU already
exists" are answers; "Something went wrong" is not. A `ProblemDetails.detail` is already what axios
puts in the error body.

**This is the first place in the storefront that shows a server message to a person**, so it gets a
shared component rather than a paragraph in each form.

## D5 - Does the seller set stock?

**Decision**: no, and the page says so.

Inventory owns stock and nothing in it knows what a seller is. Giving a seller `PUT /api/stock/{id}`
means deciding whether a seller may set stock on a variant they do not own - which is the specs/027
ownership question again, in a service that has never heard of sellers. A product listed through
this page therefore has zero stock and reads `OutOfStock` until an administrator sets it, and the
page states that plainly instead of letting a seller wonder why nobody is buying.

Recorded as the next thing this feature will be asked for.
