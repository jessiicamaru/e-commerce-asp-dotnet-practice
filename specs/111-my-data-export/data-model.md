# Data model: A person downloads the data the shop holds about them

**No schema change.** The feature only reads. Below is the inventory each service declares (FR-002).

## Identity

| Table | Declared | Section / reason |
| :-- | :-- | :-- |
| `users` | exported | `profile`: email, names, phone, language, created, email confirmed, two-factor on, roles. Never the password hash or TOTP secret. |
| `roles`, `user_roles` | exported (as `profile.roles`) / not personal | the role names |
| `delivery_addresses` | exported | `addresses` |
| `seller_profiles` | exported | `shop` |
| `seller_payout_accounts` | exported | `payoutAccount`, masked |
| `shop_applications` | exported | `shopApplications` |
| `outgoing_emails` | exported | `emails`: template, status, created, sent; never the data or body |
| `refresh_tokens`, `password_reset_tokens`, `email_confirmation_tokens`, `two_factor_challenges`, `two_factor_recovery_codes`, `sign_in_throttles` | withheld | secrets or security counters |
| `email_template_versions` | not personal | staff-edited words |

## Catalog

| Table | Declared | Section / reason |
| :-- | :-- | :-- |
| `reviews` | exported | `reviews` |
| `product_questions` | exported | `questions` (with the seller's answer) |
| `saved_products` | exported | `savedProducts` |
| `content_reports` | exported | `reports` (the ones they filed) |
| `review_eligibility` | exported | `reviewEligibility` |
| `sellers` | exported | `shop` (a seller's shop state) |
| `products` | exported | `products` (a seller's own listings: id, name, SKU, review status) |
| `product_viewers` | withheld | cannot be linked to the person |
| `categories`, `product_variants`, `variant_prices`, `variant_options`, translations, `product_views` | not personal | |

## Order

| Table | Declared | Section / reason |
| :-- | :-- | :-- |
| `orders`, `order_items` | exported | `orders`, with the lines and the frozen delivery copy |
| `order_shipments` | exported | `orders[].parcels` for the buyer; a seller's sales withheld |
| `parcel_returns` | exported | `returns` |
| `voucher_redemptions`, `voucher_customer_uses` | exported | `voucherUses` |
| `vouchers` | exported | `vouchers` (a seller's own) |
| `payouts` | exported | `payouts` (a seller's own) |
| `delivery_options`, `delivery_option_prices`, `carriers`, voucher conditions/targets/amounts | not personal | |

## Cart, Payment, Activity

| Service | Table | Declared |
| :-- | :-- | :-- |
| Cart | `carts`, `cart_lines` | exported (`cart`) |
| Cart | `checkout_outcomes` | withheld: working records; the order is in Order's export |
| Payment | `payments`, `refunds` | exported |
| Activity | `notifications` | exported |
| Activity | `audit_entries` | exported (own actions, decisions about them - no staff identities, no snapshots) |
| Activity | `notification_wording_versions` | not personal |

The outbox tables of each service (`OutboxMessage`, `OutboxState`, `InboxState`) are declared not personal: they are
the broker's delivery bookkeeping, emptied as messages are delivered.

**Inventory** keeps no person's id. **The orchestrator's** saga state holds the buyer's id while an order settles, and is
withheld as working state. Neither has an export.
