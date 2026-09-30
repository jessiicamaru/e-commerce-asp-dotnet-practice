# Data model: A person deletes their account

## Identity

| Change | Detail |
| :-- | :-- |
| `users.DeletedAt` | `timestamp with time zone NULL` - new, migration `AddAccountDeletion`. Expand only: an older image ignores it. |

A deleted row: `Email = deleted-<id:N>@deleted.invalid`, `FirstName = ''`, `LastName = ''`, `PhoneNumber = NULL`,
`PasswordHash` = a hash of 32 random bytes, `TwoFactorSecret`/`TwoFactorEnabledAt`/`TwoFactorLastStep` = NULL,
`EmailConfirmedAt = NULL`, `Language = NULL`, no `user_roles`.

Deleted rows: `delivery_addresses`, `seller_profiles`, `seller_payout_accounts`, `shop_applications`,
`outgoing_emails` (recipient), `refresh_tokens`, `password_reset_tokens`, `email_confirmation_tokens`,
`two_factor_challenges`, `two_factor_recovery_codes`, `sign_in_throttles` (by the email's key).

## Catalog (on `AccountDeleted`)

| Table | Action |
| :-- | :-- |
| `product_reviews` | `AuthorName = ''` where `CustomerId` = the person |
| `product_questions` | `AskerName = ''` where `AskerId` = the person |
| `saved_products`, `review_eligibility` | deleted (`CustomerId`) |
| `content_reports` | deleted (`ReporterId`) |
| `sellers` | `ClosedAt = now`, `ClosedReason = 'The account was deleted'`, `Description = NULL`; then `ApplyShopStateAsync` |

## Order (on `AccountDeleted`)

| Table | Action |
| :-- | :-- |
| `orders` | the delivery copy: `RecipientName = ''`, `Line1 = ''`, `Line2 = NULL`, `City = ''`, `Region = NULL`, `PostalCode = ''`, `Phone = NULL`; `Country` kept |
| `parcel_returns` | `Reason = ''` where `CustomerId` = the person |
| `vouchers` | `Status = 'Disabled'` where `SellerId` = the person |
| `payouts` | `PaidToHolder = NULL` where `SellerId` = the person |

## Cart (on `AccountDeleted`)

`carts` (and `cart_lines`, by cascade) and `checkout_outcomes` of the person: deleted.

## Activity (on `AccountDeleted`)

| Table | Action |
| :-- | :-- |
| `notifications` | deleted (`RecipientId`) |
| `audit_entries` | `ActorEmail = NULL` where `ActorId` = the person; the email replaced by `a deleted account` in `Summary` where the actor, the subject or `AboutUserId` is the person; `Before = NULL`, `After = NULL`, `Changes = '[]'`, `ChangeCount = 0` where the subject is the person's `User` row |

## Payment

No change: `payments` and `refunds` hold ids and amounts.

## The inventory (`PersonalDataInventory.Kept`)

| Service | Kept sections |
| :-- | :-- |
| Identity | `profile` (the anonymised row) |
| Catalog | `reviews`, `questions`, `products`, `shop` |
| Order | `orders`, `returns`, `voucherUses`, `voucherUseCounts`, `vouchers`, `payouts` |
| Cart | none |
| Payment | `payments`, `refunds` |
| Activity | `activity` |

## States

A user row: active → (locked / banned, as before) → **deleted**. Deleted is final and has no way back.
