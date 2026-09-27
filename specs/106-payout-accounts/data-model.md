# Data Model: Sellers say where their payouts go

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27

## Identity: `seller_payout_accounts` (migration `AddSellerPayoutAccounts`)

| Column | Type | Notes |
| :-- | :-- | :-- |
| `SellerId` | `uuid` PK | The seller's user id. |
| `BankName` | `varchar(100)` | |
| `AccountHolder` | `varchar(100)` | |
| `AccountNumber` | `varchar(34)` | Letters and digits, 6-34 characters, stored without spaces. 34 is the longest IBAN. |
| `UpdatedAt` | `timestamptz` | Drives the "changed recently" warning (research D3). |

## Order: `payouts` (migration `AddPayoutDestination`)

| Column | Type | Notes |
| :-- | :-- | :-- |
| `PaidToBank` | `varchar(100)` null | **New.** Frozen when the payout is recorded. |
| `PaidToHolder` | `varchar(100)` null | **New.** |
| `PaidToAccountLast4` | `varchar(4)` null | **New.** Never the full number (research D2). |

All three are null for payouts from before this feature. Both migrations are expand only.

## Email

`PayoutAccountChanged`, with placeholders `name`, `bank` and `last4`, in the reader's language (`ReadersLanguage`,
specs/083).

## State transitions

None.
