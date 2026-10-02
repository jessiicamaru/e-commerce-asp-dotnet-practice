# Contracts: A separate token audience for the back office

## Access tokens

| Session (specs/138) | `aud` |
| :-- | :-- |
| Storefront | `JwtSettings:Audience` (`EcommerceClients`) |
| Back office | `JwtSettings:BackOfficeAudience` (`EcommerceBackOffice` by default) |

Every service accepts either. `Admin`/`Moderator` count only in a token whose `aud` is the back office's.

No HTTP, message or gRPC shape changes.
