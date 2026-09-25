# Tasks: A stopped account's access token stops working within seconds

- [X] T001 [US1] [US2] Tests first. Cover:
  - the rule and pruning, in `Ecommerce.Identity.Tests/AccessTokenRevocationTests.cs`;
  - the consumer recording a revocation;
  - each of the six Identity actions publishing `AccessTokensRevoked`;
  - `OnTokenValidated` failing a revoked token and passing a later one.
- [X] T002 [US1] The contract, `RevokedAccessTokens`, the `OnTokenValidated` hook and the consumer with its registration extension.
- [X] T003 [US1] Identity publishes in all six places. Every service registers the consumer.
- [X] T004 End to end:
  - ban a customer holding a token, and see Cart, Order, Catalog and Identity refuse it within seconds;
  - change a password, and see the storefront session refresh.
- [X] T005 Mutation checks. Docs:
  - security, messages and reference;
  - timeline, backlog, decisions and counts;
  - CLAUDE.md.
