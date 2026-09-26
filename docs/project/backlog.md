# Backlog

What the system does not do yet, as open GitHub issues, grouped by priority. Each issue carries the
evidence it was found with (a search of the code, a request against the running stack) and its
acceptance criteria. This page is the summary; the issue is the source of truth - close the issue and
update this page in the same change.

Last reviewed: 2026-09-24 (after #119-#128 and #132).

## Deliberately deferred

Not issues, by decision:

- **A real payment provider.** Payment is a stub that approves without moving money
  (`Provider = "Stub"` on every row, a startup warning, `/health` says so). `StubPaymentGateway` is the
  seam a real integration replaces.
- **Deployment to an environment.** Images are published to GHCR with immutable tags; nothing deploys
  them.

## Priority 0 - defects in what is built

Found while writing the feature documents, which describe the code as it is: places where the code
disagrees with its own rules. **All ten (#119-#128) are fixed** - see Fixed below.

Found on 2026-09-27 while the design records were rebuilt from the code:

| Issue | Title |
| :-- | :-- |

Found on 2026-09-27 in an audit of the management features and delivery:

| Issue | Title |
| :-- | :-- |
| [#193](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/193) | A banned seller's products stay on sale |

## Priority 1 - accounts and security

A person cannot recover or manage their own account, and sign-in can be guessed at freely.

| Issue | Title |
| :-- | :-- |

## Priority 2 - buying, selling and running the shop

Gaps a shopper, a seller or the shop's staff would notice. Found on 2026-09-27 in an audit of the management features
(administrator, moderator, seller) and delivery. Delivery assumes **one carrier** - decided with the user; a courier
role and several carriers are left out.

| Issue | Title |
| :-- | :-- |
| [#194](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/194) | Staff cannot find an order outside the fulfilment queue |
| [#195](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/195) | Administrators have no screen to manage categories |
| [#196](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/196) | Delivery options and the carrier can only be changed by redeploying |
| [#197](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/197) | A shopper cannot see a seller's shop |
| [#198](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/198) | A moderator decides a lock without seeing the person's history |
| [#199](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/199) | Shoppers cannot report a review, a question or a product |
| [#200](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/200) | A seller is not told when a variant runs low |


## Priority 3 - technical debt

Known and recorded; they hurt at scale or in operation, not today.

Nothing open.

## Priority 4 - testing

| Issue | Title |
| :-- | :-- |

## Fixed

Closed since the backlog was written, newest first. The timeline has the full history.

| Issue | Title | Fixed by |
| :-- | :-- | :-- |
| [#186](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/186) | Behaviour that is fixed or promised but not held by a test | [#201](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/201) (specs/094) |
| [#184](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/184) | During a rollback a seller's new product goes on sale unreviewed | [#192](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/192) (specs/093) |
| [#185](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/185) | Off the shelf is IsListed for reads but IsListed and IsActive for writes | [#191](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/191) (specs/092) |
| [#182](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/182) | A saved product back on sale by any route but Inventory's tells nobody | [#190](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/190) (specs/091) |
| [#181](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/181) | Deleting a product leaves its stock reservations behind | [#189](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/189) (specs/090) |
| [#183](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/183) | The auth endpoints are public only because they lack [Authorize] | [#188](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/188) (specs/089) |
| [#180](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/180) | A moderator can shorten an administrator's lock by locking the account again | [#187](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/187) (specs/088) |
| [#113](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/113) | Search scans every product | [#158](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/158) (specs/074) |
| [#109](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/109) | A shopper cannot save a product for later | [#159](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/159) (specs/075) |
| [#110](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/110) | Nobody can ask a seller about a product | [#160](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/160) (specs/076) |
| [#150](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/150) | Email templates and notification wording cannot be edited | [#161](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/161) (specs/077), [#162](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/162) (specs/078) |
| [#114](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/114) | Product images only work with one Catalog instance | [#163](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/163) (specs/079) |
| [#117](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/117) | No test drives the storefront in a browser | [#164](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/164) (specs/080) |
| [#166](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/166) | A product that is not on sale still serves its image, reviews and questions | [#169](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/169) (specs/081) |
| [#168](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/168) | Insights count UTC days, so a Vietnamese morning lands on yesterday | [#170](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/170) (specs/082) |
| [#167](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/167) | People are emailed only about a paid order, a reset and a confirmation | [#171](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/171) (specs/083) |
| [#172](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/172) | The admin Overview counts a returned and refunded parcel as revenue | [#176](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/176) (specs/084) |
| [#174](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/174) | A product that is off the shelf still accepts new reviews | [#177](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/177) (specs/085) |
| [#173](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/173) | Anybody can inflate a product's view count - the view endpoint has no limit and no dedup | [#178](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/178) (specs/086) |
| [#175](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/175) | Nobody can see an email that failed to send | [#179](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/179) (specs/087) |
| [#118](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/118) | Every Bruno and verify-saga run leaves products behind | [#157](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/157) (specs/073) |
| [#116](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/116) | Revenue is counted on the day an order was placed, not paid | [#156](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/156) (specs/072) |
| [#115](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/115) | The orchestrator has no health endpoint | [#155](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/155) (specs/071) |
| [#108](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/108) | There is no way to give a discount | [#153](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/153) (specs/069, server) and [#154](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/154) (specs/070, screens) |
| [#111](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/111) | A seller cannot see how their shop is doing | [#152](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/152) (specs/068) |
| [#107](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/107) | A delivered parcel cannot be returned | [#149](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/149) (specs/066, server) and [#151](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/151) (specs/067, screens) |
| [#112](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/112) | A lock or ban takes up to 15 minutes to reach a signed-in session | [#148](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/148) (specs/065) |
| [#104](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/104) | Nobody can change their password or their name | [#147](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/147) (specs/064) |
| [#106](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/106) | An email address is never confirmed to belong to anyone | [#146](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/146) (specs/063) |
| [#105](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/105) | Sign-in can be guessed at without any limit | [#145](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/145) (specs/062) |
| [#103](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/103) | A forgotten password cannot be reset | [#144](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/144) (specs/061) |
| [#102](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/102) | The system sends no email at all | [#143](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/143) (specs/060) |
| [#128](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/128) | Some writes record no audit entry and some events tell nobody | [#141](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/141), [#142](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/142) (specs/058, 059) |
| [#127](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/127) | Two first reviews at once give a 500, and hiding a review is not guarded | [#140](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/140) (specs/057) |
| [#126](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/126) | Some seller edits of what a shopper reads skip product review | [#139](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/139) (specs/056) |
| [#125](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/125) | The insights' period rules differ between endpoints, and the chart drops the first day | [#138](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/138) (specs/055) |
| [#124](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/124) | A variant's availability can be overwritten by an older announcement | [#137](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/137) (specs/054) |
| [#123](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/123) | A payment answered after the stock hold expired still pays the order | [#135](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/135) (specs/053) |
| [#122](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/122) | An order can remove the wrong cart line when two variants of one product are in the cart | [#134](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/134) (specs/052) |
| [#132](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/132) | The storefront has no image and cannot be served as built | [#133](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/133) (specs/051) |
| [#121](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/121) | A moderator can unlock any account, including one an administrator locked | [#131](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/131) (specs/050) |
| [#120](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/120) | The sign-in page hides why a locked or banned account was refused | [#130](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/130) (specs/049) |
| [#119](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/119) | Five notification kinds show their placeholders instead of words | [#129](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/129) (specs/048) |

## Suggested order

The defects are done, email exists (#102), a password can be reset (#103), guessing is limited (#105), addresses are confirmed (#106) a person manages their own account (#104) and a stop reaches a signed-in session in seconds (#112). Priority 1 is done, and returns (#107) and seller insights (#111). Vouchers (#108) are done, and so are saved products (#109), product questions (#110), editable emails and notices (#150) and object storage (#114);
and the storefront is tested in a browser (#117). Filed 2026-09-26 from the features' known limits: #166, then #167 and #168. Those are done, and so are #172, #174, #173 and #175, filed 2026-09-27. Next, filed 2026-09-27 from the design-record rebuild: #180 and #183 first (security, both done), then #181, #182, #185, #184 and #186 - all done. Next, from the 2026-09-27 audit: #193 (a defect), then #194, #195, #196, #197, #198, #199 and #200.
