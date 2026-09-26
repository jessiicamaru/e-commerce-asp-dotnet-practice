# Feature Specification: Emails for what happens to people

**Feature Branch**: `083-more-emails` | **Created**: 2026-09-26 | **Issue**: #167 (closes it)

## Why

Three emails existed: the order confirmation, the password reset and the address confirmation (specs/060, 061,
063). Everything else that happened to a person reached them only as a notice in the bell, which they see only if
they open the shop:
- their parcel shipped;
- their order was cancelled;
- a return was decided or refunded;
- a product they saved came back;
- their account was locked or banned. Signed out by it, they cannot open the bell at all.

## Requirements

- **FR-001** Eight new emails, each with built-in Vietnamese and English words, declared placeholders, sample data
  for the console's preview, and editable at `/admin/emails` like the first three (specs/077):

  | Email | Placeholders |
  | :-- | :-- |
  | `ParcelShipped` | `{name}`, `{order}`, `{shop}`, `{tracking}`, `{link}` |
  | `OrderCancelled` | `{name}`, `{order}`, `{total}`, `{link}` |
  | `ReturnAccepted` | `{name}`, `{order}`, `{link}` |
  | `ReturnRefused` | `{name}`, `{order}`, `{reason}`, `{link}` |
  | `ReturnRefunded` | `{name}`, `{order}`, `{amount}`, `{link}` |
  | `SavedBackInStock` | `{name}`, `{product}`, `{link}` |
  | `AccountLocked` | `{name}`, `{until}`, `{reason}` |
  | `AccountBanned` | `{name}`, `{reason}` |

- **FR-002** Each is asked for **where the notice is sent, in the same transaction**, through the outbox
  (`IEmailSender`). A change that did not happen asks for nothing: a refused move, a repeated cancellation, a
  product off the shelf coming back.
- **FR-003** **Language.**
  - An email about an order is written in the order's language (specs/021).
  - The others are written in **the language the person last used the shop in**. Identity records it as
    `users.Language` at sign-up, at sign-in and at each session renewal, from `Accept-Language`.
  - A sender that cannot know the language asks for `EmailTemplate.ReadersLanguage`, the empty string. Identity
    fills it in when it queues the email, falling back to Vietnamese. Catalog is such a sender: it does not know
    who a saver is.
  - Only a language an email is written in is recorded. "en-US" counts as "en"; anything else changes nothing.

## Decisions

- **The account learns its language; there is no setting.** A setting is a screen nobody visits. The storefront
  already sends `Accept-Language` on every request, and a renewal every few minutes carries a switch to the
  emails. It costs one guarded `UPDATE`, which writes nothing when the value is unchanged.
- **Returns are three emails, not one "decided" email.** The issue named a single `ReturnDecided`. An editable
  template with a condition inside it cannot be edited sensibly, so accepted, refused and refunded are separate,
  each with its own words.
- **The cancellation email says the whole total is refunded**, which is what specs/039 does. It does not say who
  cancelled, so the words need no per-actor wording.
- **A lock's end time is written in UTC and says so.** An email has no reader's time zone.
- **No opting out.** These are transactional. Back in stock stops when the product is no longer saved.

## Out of scope

- A language setting on the account page.
- Emails to sellers (a new sale, a payout): they read the shop console daily.
- Product names in the saver's language: the email uses the product's default-language name, as the notice does.
