# Phase 0 Research: Delete a Product

> Written on 2026-09-27, after the feature merged (#61), from the code at that merge, the pull request and docs/features/catalog.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-22

No separate design record was written while the feature was built; these decisions are reconstructed
from the code, its comments and the pull request. Who made each one is not recorded beyond the PR
itself.

---

## D1 - Delete, not deactivate

**Decision**: A hard delete of the product and every variant, Admin only, kept distinct from
deactivation.

**Rationale**: Deactivation already existed and is the right way to stop selling something real: the
row stays, a cart holding it can explain itself, and a report still balances. The rows this feature was
for were never real - `E2E Widget 17900854702164`, `iPhone 16 Pro Max 1790085352812` - and hiding them
would leave them in every administrator list and every future report. The controller, the command and
the Bruno request all say "not the way to stop selling something" in so many words, so the next person
does not reach for the wrong tool.

**Alternatives considered**:

- **Deactivate the debris.** Rejected: it takes them off sale but keeps them in the catalogue, which
  was the problem.
- **A soft-delete flag (`DeletedAt`) filtered out everywhere.** Rejected: every query in Catalog would
  need the filter, one missed filter resurrects the row, and the SKU stays taken.
- **Raw SQL against Catalog's database.** What cleaning meant before this feature. Rejected: it bypasses
  authorization, skips Inventory entirely, and is exactly the cross-boundary write the constitution
  forbids.

---

## D2 - Variants are removed explicitly, not by cascade

**Decision**: `ProductRepository.Remove` calls `RemoveRange(product.Variants)` and then
`Remove(product)`. The variant → product foreign key stays `RESTRICT`.

**Rationale**: The key is `RESTRICT` on purpose (specs/020): an order refers to a variant, so the
schema should not let a product take its variants down by accident. Deleting them here is saying it on
purpose, in one place. Everything below a variant - `variant_options`, `variant_prices`,
`variant_option_translations` - and the product's `product_translations` already cascade, so those go
without being named. `A_deleted_product_is_gone_along_with_everything_that_described_it` checks each.

**Alternatives considered**:

- **Change the foreign key to `CASCADE`.** Rejected: it would make every future product delete - including
  one written by mistake - silently take the variants, and it is a schema change for no behavioural
  gain.

---

## D3 - The announcement carries every variant id

**Decision**: `ProductDeletedEvent(Guid ProductId, IReadOnlyList<Guid> VariantIds, DateTime DeletedAt)`,
with the variant ids collected **before** the delete.

**Rationale**: Inventory counts stock per variant, and only a product's first variant reuses the
product's id (specs/020). An event carrying the product id alone would drop the body-only count and
leave the kit's units on the shelf for ever. The ids are read before `Remove` because afterwards there
is nothing left to read them from. `Deleting_it_announces_every_variant_so_Inventory_can_forget_them`
asserts both ids.

**Alternatives considered**:

- **Product id only, and Inventory works out the variants.** Rejected: Inventory has no idea which
  variant belongs to which product; it only has variant ids in a column still named `ProductId`.
- **One `ProductVariantDeletedEvent` per variant.** Rejected: more messages for one fact, and a product
  is deleted as one unit.

---

## D4 - Orders and carts are not told

**Decision**: Only Inventory consumes the event. Order and Cart change nothing.

**Rationale**: Every order froze the name, price, SKU and option summary of what it bought (specs/009,
020, 021, 022), so it describes the purchase perfectly well after the catalogue forgets the product -
an order that changed because a catalogue row was deleted would be the defect. Cart already shows a line
whose variant Catalog no longer has as `NoLongerAvailable` (specs/010), and checkout's pricing call
refuses an order that names an unknown variant.

**Alternatives considered**:

- **Refuse to delete a product that has orders.** Rejected: the debris products had orders from the
  saga checks, and nothing about those orders depends on the catalogue row.
- **Remove the product from every cart.** Rejected: a line that vanishes without explanation is worse
  than one marked unavailable, and Cart already does the latter.

---

## D5 - Inventory forgets with one statement, and the consumer must be registered

**Decision**: `ForgetProductCommand` calls `StockRepository.ForgetAsync`, one
`ExecuteDeleteAsync` over `stock_items` where `ProductId` is in the announced ids, returning the count.
The consumer is registered in `Program.cs` with a comment saying why that line matters.

**Rationale**: There is nothing to decide, so there is nothing to read first: a row that is not there
was already forgotten, which is the answer the caller wanted either way. That makes the consumer
idempotent without a guard table - a second delivery affects zero rows (`Forgetting_twice_is_a_no_op_because_a_message_redelivers`).

The registration is the part that went wrong. The consumer was written and not added to
`AddMassTransit`, and **an unregistered MassTransit consumer is silent**: no queue, no error, no log. It
was found only by counting stock rows (125) against variants (23) after the cleaner had already deleted
97 products. Those deletions left their stock rows behind; they were not cleaned up, and the PR says so
rather than leaving a number that does not add up. The `Program.cs` comment now reads "A consumer that is
written and not registered here NEVER RUNS AND NEVER COMPLAINS".

**Alternatives considered**:

- **Load the rows and remove them through the change tracker.** Rejected: a read for no decision, and a
  second round trip.
- **Keep the rows at zero on hand.** Rejected: zero means "the catalogue still sells it and there are
  none left", a different fact and the one a shopper would be shown. `A_forgotten_variant_has_no_stock_to_answer_for`
  asserts 404, not zero.

---

## D6 - The cleaner keeps a list rather than deleting a pattern

**Decision**: `seed/clean-test-debris.py` keeps every product whose SKU `cameras.json` names and deletes
the rest, through `DELETE /api/products/{id}` as an administrator, printing what it would do and doing
nothing without `--yes`.

**Rationale**: A keep list cannot miss a new kind of debris; a list of patterns to delete (`E2E Widget`,
`BRUNO-`) can, the first time a script names its products differently. Going through the API means each
deletion is authorised, announced to Inventory, and identical to what a person would do. A 204 is
returned as `True` rather than `None` so a failure and an empty success are not counted alike.

**Alternatives considered**:

- **A delete-pattern list.** Rejected for the reason above.
- **SQL.** Rejected: skips Inventory and authorization (D1).
- **Make the scripts clean up after themselves.** Right, but a separate change to three scripts; it
  landed later in specs/073 (#118).
