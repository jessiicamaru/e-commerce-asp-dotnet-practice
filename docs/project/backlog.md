# Backlog

What the system does not do yet, as open GitHub issues, grouped by priority. Each issue carries the
evidence it was found with (a search of the code, a request against the running stack) and its
acceptance criteria. This page is the summary; the issue is the source of truth - close the issue and
update this page in the same change.

Last reviewed: 2026-09-24.

## Deliberately deferred

Not issues, by decision:

- **A real payment provider.** Payment is a stub that approves without moving money
  (`Provider = "Stub"` on every row, a startup warning, `/health` says so). `StubPaymentGateway` is the
  seam a real integration replaces.
- **Deployment to an environment.** Images are published to GHCR with immutable tags; nothing deploys
  them.

## Priority 0 - defects in what is built

Found while writing the feature documents, which describe the code as it is: places where the code
disagrees with its own rules. Fix these before building anything new.

| Issue | Title |
| :-- | :-- |
| [#120](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/120) | The sign-in page hides why a locked or banned account was refused |
| [#121](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/121) | A moderator can unlock any account, including one an administrator locked |
| [#122](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/122) | An order can remove the wrong cart line when two variants of one product are in the cart |
| [#123](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/123) | A payment answered after the stock hold expired still pays the order |
| [#124](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/124) | A variant's availability can be overwritten by an older announcement |
| [#125](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/125) | The insights' period rules differ between endpoints, and the chart drops the first day |
| [#126](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/126) | Some seller edits of what a shopper reads skip product review |
| [#127](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/127) | Two first reviews at once give a 500, and hiding a review is not guarded |
| [#128](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/128) | Some writes record no audit entry and some events tell nobody |

## Priority 1 - accounts and security

A person cannot recover or manage their own account, and sign-in can be guessed at freely.

| Issue | Title | Depends on |
| :-- | :-- | :-- |
| [#102](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/102) | The system sends no email at all | - |
| [#103](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/103) | A forgotten password cannot be reset | #102 |
| [#104](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/104) | Nobody can change their password or their name | - |
| [#105](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/105) | Sign-in can be guessed at without any limit | - |
| [#106](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/106) | An email address is never confirmed to belong to anyone | #102 |
| [#112](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/112) | A lock or ban takes up to 15 minutes to reach a signed-in session | - |

## Priority 2 - buying and selling

Gaps a shopper or a seller would notice.

| Issue | Title |
| :-- | :-- |
| [#107](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/107) | A delivered parcel cannot be returned |
| [#108](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/108) | There is no way to give a discount |
| [#111](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/111) | A seller cannot see how their shop is doing |
| [#109](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/109) | A shopper cannot save a product for later |
| [#110](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/110) | Nobody can ask a seller about a product |

## Priority 3 - technical debt

Known and recorded; they hurt at scale or in operation, not today.

| Issue | Title |
| :-- | :-- |
| [#113](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/113) | Search scans every product |
| [#114](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/114) | Product images only work with one Catalog instance |
| [#115](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/115) | The orchestrator has no health endpoint |
| [#116](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/116) | Revenue is counted on the day an order was placed, not paid |

## Priority 4 - testing

| Issue | Title |
| :-- | :-- |
| [#117](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/117) | No test drives the storefront in a browser |
| [#118](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/118) | Every Bruno and verify-saga run leaves products behind |

## Fixed

Closed since the backlog was written, newest first. The timeline has the full history.

| Issue | Title | Fixed by |
| :-- | :-- | :-- |
| [#119](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/119) | Five notification kinds show their placeholders instead of words | [#129](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/129) (specs/048) |

## Suggested order

The defects first - #120 and #121 are small and visible, #122 and #123 can remove the wrong cart
line or sell stock the shop no longer holds. Then email (#102), which unblocks two others: #102 -> #103 -> #104 -> #105 ->
#107 -> #108 -> #111, and the rest as they become pressing.
