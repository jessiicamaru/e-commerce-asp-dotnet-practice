# Backlog

What the system does not do yet, as open GitHub issues, grouped by priority. Each issue carries the
evidence it was found with (a search of the code, a request against the running stack) and its
acceptance criteria. This page is the summary; the issue is the source of truth - close the issue and
update this page in the same change.

Last reviewed: 2026-10-06 (#353).

## Deliberately deferred

Not issues, by decision:

- **A real payment provider** - no longer deferred: #289 (VNPay sandbox), 2026-10-03.
- **Deployment to an environment** - no longer deferred: #287, #288, 2026-10-03.

## Priority 0 - defects in what is built

Found while writing the feature documents, which describe the code as it is: places where the code
disagrees with its own rules. **All ten (#119-#128) are fixed** - see Fixed below.

Found on 2026-09-27 while the design records were rebuilt from the code:

| Issue | Title |
| :-- | :-- |

Found on 2026-09-27 in an audit of the management features and delivery: #193, done - see Fixed below.

Found on 2026-10-03 and 2026-10-06 by the load test of #290 and the resilience runs of #291 - measurement finding what the
tests could not: #304 and #353, done - see Fixed below.

## Priority 1 - accounts and security

A person cannot recover or manage their own account, and sign-in can be guessed at freely.

| Issue | Title |
| :-- | :-- |

## Priority 2 - buying, selling and running the shop

Gaps a shopper, a seller or the shop's staff would notice. Found on 2026-09-27 in an audit of the management features
(administrator, moderator, seller) and delivery. Delivery assumes **one carrier** - decided with the user; a courier
role and several carriers are left out.

The first audit's #193-#200 are done, and so is the second's #217-#222 (2026-10-01, see below).

**From the screen review of 2026-10-01**: every storefront screen (64) captured in a browser against the stack with
live data, as a customer, a new seller and the administrator, at desktop and phone widths. #237-#245 first (defects:
things hidden, clipped, untranslated or wrong), then #246-#254 (usability).

| Issue | Title |
| :-- | :-- |


## Priority 2a - the back office

Decided with the user on 2026-10-02: the admin and moderator console leaves the shop - now the **storefront** - for a
**back office** of its own at `portal.ecommerce.com` ([ADR-003](../architecture/adr-003-storefront-and-back-office.md)). Staff do not shop, and a staff session should not share an origin with user-written content.
Two SPAs in one monorepo, not module federation. **All six are done** (2026-10-03): workspaces (#275), the back office
and its sign-in (#276), the console moved (#277), staff roles only in a back-office session (#278), a separate token
audience held by every service (#280) and a one-time handoff from the storefront (#279) - see Fixed below and the
progress table in ADR-003.

## Priority 2b - ready to defend

Planned with the user on 2026-10-03, once the backlog was empty: what a graduation thesis still needs - a running
deployment over HTTPS, a real payment flow (VNPay sandbox, replacing the stub), measured evidence for the evaluation
(load, resilience, metrics), automated security checks, and the report's top-down documents. In this order.

| Issue | Title |
| :-- | :-- |

## Priority 3 - technical debt

Known and recorded; they hurt at scale or in operation, not today.

Nothing open.

## Priority 4 - testing

| Issue | Title |
| :-- | :-- |
| [#364](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/364) | Two Bruno tests fail on a fresh stack - payment export sections, sellers counted |

## Fixed

Closed since the backlog was written, newest first. The timeline has the full history.

| Issue | Title | Fixed by |
| :-- | :-- | :-- |
| [#196](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/196) | Delivery options and the carrier can only be changed by redeploying | [#205](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/205) (specs/098) |
| [#197](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/197) | A shopper cannot see a seller's shop | [#206](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/206) (specs/099) |
| [#198](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/198) | A moderator decides a lock without seeing the person's history | [#207](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/207) (specs/100) |
| [#199](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/199) | Shoppers cannot report a review, a question or a product | [#208](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/208) (specs/101) |
| [#200](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/200) | A seller is not told when a variant runs low | [#209](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/209) (specs/102) |
| [#210](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/210) | A missing commission rate fails the first checkout, not startup | [#223](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/223) (specs/103) |
| [#211](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/211) | A seller cannot cancel the part of an order they cannot fulfil | [#224](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/224) (specs/104) |
| [#212](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/212) | A mistyped tracking reference can never be corrected | [#225](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/225) (specs/105) |
| [#213](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/213) | Sellers have nowhere to receive their payouts | [#226](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/226) (specs/106) |
| [#214](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/214) | A shop can be closed only by banning its seller, and a seller cannot pause it | [#227](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/227) (specs/107) |
| [#215](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/215) | A seller has no list of the returns of their parcels | [#228](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/228) (specs/108) |
| [#216](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/216) | The catalogue cannot be filtered by price or by what is in stock | [#229](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/229) (specs/109) |
| [#218](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/218) | Staff accounts sign in with a password alone | [#230](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/230) (specs/110) |
| [#217](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/217) | A person cannot delete their account or download their data | [#231](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/231) (specs/111), [#232](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/232) (specs/112) |
| [#219](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/219) | A voucher cannot be edited | [#233](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/233) (specs/113) |
| [#220](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/220) | A shopper cannot see which vouchers they could use | [#234](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/234) (specs/114) |
| [#222](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/222) | Nobody is told when an email fails for good | [#235](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/235) (specs/115) |
| [#221](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/221) | The audit log and notifications grow without bound | [#236](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/236) (specs/116) |
| [#237](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/237) | The seeded administrator is asked for a code nobody enrolled | [#255](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/255) (no spec: a development tool) |
| [#238](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/238) | The seller sidebar covers the page when the shop name is long | [#256](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/256) (specs/117) |
| [#239](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/239) | Order lines clip their totals in narrow containers | [#257](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/257) (specs/118) |
| [#242](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/242) | The cart badge still counts what was just paid for | [#258](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/258) (specs/119) |
| [#243](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/243) | The not-found page is one untranslated line | [#259](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/259) (specs/120) |
| [#244](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/244) | Staff screens show raw action names and an out-of-date status page | [#260](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/260) (specs/121) |
| [#245](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/245) | The catalogue on a phone is one long column | [#261](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/261) (specs/122) |
| [#241](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/241) | Deleted accounts are listed as Active with actions | [#262](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/262) (specs/123) |
| [#240](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/240) | A seller cannot edit what they listed - details, translations, variants | [#263](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/263) (specs/124) |
| [#251](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/251) | Product photographs are letterboxed in square frames | [#264](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/264) (specs/125) |
| [#252](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/252) | A signed-out shopper is not offered Add to cart | [#265](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/265) (specs/126) |
| [#254](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/254) | The account menu differs between pages and devices | [#266](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/266) (specs/127) |
| [#250](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/250) | Notice wording is one 7,000px page | [#267](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/267) (specs/128) |
| [#246](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/246) | The admin console's menu is flat and shows nothing waiting | [#269](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/269) (specs/129) |
| [#268](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/268) | The moderation dashboard caches a one-row page under the review list's key (found in #246) | [#270](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/270) (specs/130) |
| [#247](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/247) | A seller's home does not say what needs them | [#271](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/271) (specs/131) |
| [#248](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/248) | Orders are identified only by a timestamp | [#272](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/272) (specs/132) |
| [#249](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/249) | Long staff lists cannot be searched or filtered | [#273](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/273) (specs/133) |
| [#253](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/253) | Checkout says nothing of payment or delivery time | [#274](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/274) (specs/134) |
| [#275](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/275) | Split the storefront into npm workspaces | [#281](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/281) (specs/135) |
| [#276](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/276) | A back-office app for staff, with its own sign-in | [#282](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/282) (specs/136) |
| [#277](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/277) | The admin and moderator console moves to the back office | [#283](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/283) (specs/137) |
| [#278](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/278) | Staff roles only in a back-office session | [#284](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/284) (specs/138) |
| [#280](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/280) | A separate token audience for the back office - deferred, the stricter second line | [#285](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/285) (specs/139) |
| [#279](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/279) | One-time handoff from the storefront to the back office - deferred until signing in twice proves a nuisance | [#286](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/286) (specs/140) |
| [#287](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/287) | A production stack served over HTTPS | [#296](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/296) (specs/141) |
| [#288](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/288) | Deploy and roll back from CI | [#297](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/297) (specs/142) |
| [#289](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/289) | Pay with VNPay (sandbox) instead of the stub | [#298](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/298) (specs/143) |
| [#299](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/299) | A paid order's stock confirmation is lost to a serialization failure under load | [#300](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/300) (specs/145) |
| [#301](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/301) | Checkouts of one product queue behind serialization retries | [#302](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/302) (specs/146) |
| [#290](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/290) | Load-test checkout and measure it | [#303](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/303) (specs/144) |
| [#291](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/291) | Resilience: checkout survives a service or the broker going down | [#305](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/305) (specs/147) |
| [#292](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/292) | Metrics: Prometheus and Grafana dashboards | [#307](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/307) (specs/148) |
| [#306](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/306) | A message redelivered mid-consume faults on the inbox's unique key (found 2026-10-05 by #292's broker-fault run) | [#308](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/308) (specs/149) |
| [#293](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/293) | Automated security scanning in CI | [#309](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/309) (specs/150) |
| [#294](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/294) | Security headers on both apps | [#325](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/325) (specs/151) |
| [#304](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/304) | After a broker outage, new orders wait a minute behind the outbox backlog | [#352](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/352) (specs/154) |
| [#353](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/353) | After a broker outage on MassTransit 8.5.11, ReserveInventory stops consuming for good | [#354](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/354) (specs/155) |
| [#359](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/359) | A shop for anything, not only cameras - copy, placeholder and a multi-vertical seed | [#360](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/360) (specs/156) |
| [#361](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/361) | Serve the catalogue's public reads from memory, evicted on every catalogue write | [#362](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/362) (specs/157) |
| [#363](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/363) | Categories in a tree - departments and their categories | [#365](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/365) (specs/158) |
| [#366](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/366) | Product specifications per category - shown, filled in, filtered on | [#367](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/367) (specs/159) |
| [#295](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/295) | The report's architecture overview, deployment guide and evaluation | [#327](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/327) (specs/152) |
| [#331](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/331) | Dependabot proposes upgrades that must not be merged, one pull request per major | [#332](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/332) (specs/153) |
| [#195](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/195) | Administrators have no screen to manage categories | [#204](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/204) (specs/097) |
| [#194](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/194) | Staff cannot find an order outside the fulfilment queue | [#203](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/203) (specs/096) |
| [#193](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/193) | A banned seller's products stay on sale | [#202](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/202) (specs/095) |
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
and the storefront is tested in a browser (#117). Filed 2026-09-26 from the features' known limits: #166, then #167 and #168. Those are done, and so are #172, #174, #173 and #175, filed 2026-09-27. Next, filed 2026-09-27 from the design-record rebuild: #180 and #183 first (security, both done), then #181, #182, #185, #184 and #186 - all done. Next, from the 2026-09-27 audit: #193 (a defect, done), then #194, #195, #196, #197, #198, #199 and #200 - all done. From the second 2026-09-27 audit: #217 (download, then delete), #219, #220, #222 and #221 - all done by 2026-10-01.
