# Data model

> **Generated** by [`docs/tools/generate_reference.py`](../tools/generate_reference.py) from commit `768bc0e`. Do not edit by hand - change the code and run the script again.

Every table in every service's database, read from the EF Core model snapshot - so it is the schema the migrations produce. Each service owns its database outright; nothing joins across them, and a value that crosses a service boundary (a product id in an order line, say) is a copy, not a foreign key. The MassTransit outbox and inbox tables (`InboxState`, `OutboxMessage`, `OutboxState`) are in every database that publishes or consumes and are listed once here rather than per service.

## Identity - `ecommerce_identity_db` (12 tables)

### `delivery_addresses`

Entity `DeliveryAddress`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `City` | character varying(100) |  |
| `Country` | character(2) |  |
| `CreatedAt` | timestamp with time zone |  |
| `IsDefault` | boolean |  |
| `Line1` | character varying(200) |  |
| `Line2` | character varying(200) | yes |
| `Phone` | character varying(30) | yes |
| `PostalCode` | character varying(16) |  |
| `RecipientName` | character varying(100) |  |
| `Region` | character varying(100) | yes |
| `UpdatedAt` | timestamp with time zone |  |
| `UserId` | uuid |  |

### `email_confirmation_tokens`

Entity `EmailConfirmationToken`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `CreatedAt` | timestamp with time zone |  |
| `ExpiresAt` | timestamp with time zone |  |
| `TokenHash` | character(64) |  |
| `UsedAt` | timestamp with time zone | yes |
| `UserId` | uuid |  |

### `email_template_versions`

Entity `EmailTemplateVersion`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `BodyHtml` | character varying(20000) | yes |
| `CreatedAt` | timestamp with time zone |  |
| `CreatedBy` | uuid |  |
| `IsDefault` | boolean |  |
| `Language` | character varying(10) |  |
| `Subject` | character varying(200) | yes |
| `Template` | character varying(50) |  |
| `Version` | integer |  |

### `outgoing_emails`

Entity `OutgoingEmail`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `Attempts` | integer |  |
| `CreatedAt` | timestamp with time zone |  |
| `DataJson` | jsonb |  |
| `Language` | character varying(10) |  |
| `LastError` | character varying(1000) | yes |
| `NextAttemptAt` | timestamp with time zone |  |
| `RecipientId` | uuid |  |
| `SentAt` | timestamp with time zone | yes |
| `Status` | character varying(20) |  |
| `Template` | character varying(64) |  |

### `password_reset_tokens`

Entity `PasswordResetToken`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `CreatedAt` | timestamp with time zone |  |
| `ExpiresAt` | timestamp with time zone |  |
| `TokenHash` | character(64) |  |
| `UsedAt` | timestamp with time zone | yes |
| `UserId` | uuid |  |

### `refresh_tokens`

Entity `RefreshToken`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `ExpiresAt` | timestamp with time zone |  |
| `ReplacedByToken` | text | yes |
| `RevokedAt` | timestamp with time zone | yes |
| `Token` | character varying(500) |  |
| `UserId` | uuid |  |

### `roles`

Entity `Role`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `Description` | character varying(255) |  |
| `Name` | character varying(50) |  |

### `seller_profiles`

Entity `SellerProfile`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `UserId` | uuid |  |
| `CreatedAt` | timestamp with time zone |  |
| `ShopName` | character varying(100) |  |
| `UpdatedAt` | timestamp with time zone |  |

### `shop_applications`

Entity `ShopApplication`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `CreatedAt` | timestamp with time zone |  |
| `DecidedAt` | timestamp with time zone | yes |
| `DecidedBy` | uuid | yes |
| `DecisionReason` | character varying(500) | yes |
| `Description` | character varying(1000) | yes |
| `Phone` | character varying(20) | yes |
| `ShopName` | character varying(100) |  |
| `Status` | character varying(20) |  |
| `UserId` | uuid |  |

### `sign_in_throttles`

Entity `SignInThrottle`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `EmailKey` | character varying(255) | yes |
| `BlockedUntil` | timestamp with time zone | yes |
| `Failures` | integer |  |
| `WindowStartedAt` | timestamp with time zone |  |

### `user_roles`

Entity `user_roles`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `role_id` | uuid |  |
| `user_id` | uuid |  |

### `users`

Entity `User`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `BanReason` | character varying(500) | yes |
| `BannedAt` | timestamp with time zone | yes |
| `CreatedAt` | timestamp with time zone |  |
| `Email` | character varying(255) |  |
| `EmailConfirmedAt` | timestamp with time zone | yes |
| `FirstName` | character varying(100) |  |
| `IsActive` | boolean |  |
| `LastName` | character varying(100) |  |
| `LockReason` | character varying(500) | yes |
| `LockedUntil` | timestamp with time zone | yes |
| `PasswordHash` | character varying(255) |  |
| `PhoneNumber` | character varying(20) | yes |
| `UpdatedAt` | timestamp with time zone |  |

## Catalog - `ecommerce_catalog_db` (14 tables)

### `categories`

Entity `Category`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `CreatedAt` | timestamp with time zone |  |
| `Description` | character varying(500) | yes |
| `IsActive` | boolean |  |
| `Name` | character varying(100) |  |
| `ParentCategoryId` | uuid | yes |
| `Slug` | character varying(150) |  |
| `UpdatedAt` | timestamp with time zone |  |

### `category_translations`

Entity `CategoryTranslation`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `CategoryId` | uuid |  |
| `Description` | character varying(500) | yes |
| `Language` | character varying(10) |  |
| `Name` | character varying(100) |  |

### `product_questions`

Entity `ProductQuestion`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `Answer` | character varying(2000) | yes |
| `AnswerHiddenAt` | timestamp with time zone | yes |
| `AnswerHiddenBy` | uuid | yes |
| `AnswerHiddenReason` | character varying(500) | yes |
| `AnswerUpdatedAt` | timestamp with time zone | yes |
| `AnsweredAt` | timestamp with time zone | yes |
| `AnsweredBy` | uuid | yes |
| `AskerId` | uuid |  |
| `AskerName` | character varying(100) |  |
| `Body` | character varying(1000) |  |
| `CreatedAt` | timestamp with time zone |  |
| `HiddenAt` | timestamp with time zone | yes |
| `HiddenBy` | uuid | yes |
| `HiddenReason` | character varying(500) | yes |
| `ProductId` | uuid |  |

### `product_reviews`

Entity `Review`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `AuthorName` | character varying(100) |  |
| `Body` | character varying(2000) | yes |
| `CreatedAt` | timestamp with time zone |  |
| `CustomerId` | uuid |  |
| `HiddenAt` | timestamp with time zone | yes |
| `HiddenBy` | uuid | yes |
| `HiddenReason` | character varying(500) | yes |
| `ProductId` | uuid |  |
| `Rating` | integer |  |
| `UpdatedAt` | timestamp with time zone |  |

### `product_translations`

Entity `ProductTranslation`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `Description` | character varying(2000) | yes |
| `Language` | character varying(10) |  |
| `Name` | character varying(200) |  |
| `ProductId` | uuid |  |

### `product_variants`

Entity `ProductVariant`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `Availability` | boolean |  |
| `AvailabilityObservedAt` | timestamp with time zone | yes |
| `CreatedAt` | timestamp with time zone |  |
| `ImageContentType` | character varying(20) | yes |
| `ImageUpdatedAt` | timestamp with time zone | yes |
| `IsActive` | boolean |  |
| `OptionSummary` | character varying(200) |  |
| `Price` | decimal(18,2) |  |
| `ProductId` | uuid |  |
| `Sku` | character varying(50) |  |
| `UpdatedAt` | timestamp with time zone |  |

### `product_views`

Entity `ProductView`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `ProductId` | uuid |  |
| `Day` | date |  |
| `Views` | integer |  |

### `products`

Entity `Product`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `Availability` | boolean |  |
| `AvailabilityObservedAt` | timestamp with time zone | yes |
| `CategoryId` | uuid |  |
| `CreatedAt` | timestamp with time zone |  |
| `Description` | character varying(2000) | yes |
| `ImageContentType` | character varying(20) | yes |
| `ImageUpdatedAt` | timestamp with time zone | yes |
| `IsActive` | boolean |  |
| `Name` | character varying(200) |  |
| `Price` | decimal(18,2) |  |
| `RatingAverage` | numeric(3,2) | yes |
| `RatingCount` | integer |  |
| `ReviewReason` | character varying(500) | yes |
| `ReviewStatus` | character varying(20) |  |
| `ReviewedAt` | timestamp with time zone | yes |
| `ReviewedBy` | uuid | yes |
| `SellerId` | uuid | yes |
| `Sku` | character varying(50) |  |
| `SubmittedAt` | timestamp with time zone | yes |
| `UpdatedAt` | timestamp with time zone |  |

### `review_eligibility`

Entity `ReviewEligibility`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `ProductId` | uuid |  |
| `CustomerId` | uuid |  |
| `FirstDeliveredAt` | timestamp with time zone |  |

### `saved_products`

Entity `SavedProduct`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `CustomerId` | uuid |  |
| `ProductId` | uuid |  |
| `SavedAt` | timestamp with time zone |  |

### `sellers`

Entity `Seller`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `SellerId` | uuid |  |
| `ObservedAt` | timestamp with time zone |  |
| `ShopName` | character varying(100) |  |

### `variant_option_translations`

Entity `VariantOptionTranslation`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `Language` | character varying(10) |  |
| `Name` | character varying(50) |  |
| `OptionId` | uuid |  |
| `Value` | character varying(100) |  |

### `variant_options`

Entity `VariantOption`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `Name` | character varying(50) |  |
| `Value` | character varying(100) |  |
| `VariantId` | uuid |  |

### `variant_prices`

Entity `VariantPrice`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `Amount` | decimal(18,2) |  |
| `Currency` | character varying(3) |  |
| `VariantId` | uuid |  |

## Cart - `ecommerce_cart_db` (3 tables)

### `cart_lines`

Entity `CartLine`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `AddedAt` | timestamp with time zone |  |
| `CartId` | uuid |  |
| `ProductId` | uuid |  |
| `Quantity` | integer |  |
| `VariantId` | uuid | yes |

### `carts`

Entity `Cart`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `CreatedAt` | timestamp with time zone |  |
| `UpdatedAt` | timestamp with time zone |  |
| `UserId` | uuid |  |

### `checkout_outcomes`

Entity `CheckoutOutcome`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `OrderId` | uuid |  |
| `Applied` | boolean |  |
| `ItemsJson` | jsonb | yes |
| `Outcome` | character varying(16) |  |
| `UpdatedAt` | timestamp with time zone |  |
| `UserId` | uuid | yes |

## Order - `ecommerce_order_db` (11 tables)

### `order_items`

Entity `OrderItem`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `OptionSummary` | character varying(200) | yes |
| `OrderId` | uuid |  |
| `PlatformDiscount` | numeric(18,2) |  |
| `ProductId` | uuid |  |
| `ProductName` | character varying(256) |  |
| `Quantity` | integer |  |
| `SellerId` | uuid | yes |
| `SellerName` | character varying(100) | yes |
| `ShopDiscount` | numeric(18,2) |  |
| `Sku` | character varying(50) | yes |
| `TaxAmount` | numeric(18,2) | yes |
| `UnitPrice` | numeric(18,2) |  |
| `VariantId` | uuid | yes |

### `order_shipments`

Entity `OrderShipment`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `Commission` | numeric(18,2) | yes |
| `DeliveredAt` | timestamp with time zone | yes |
| `DeliveryConfirmedBy` | character varying(16) | yes |
| `GoodsTotal` | numeric(18,2) | yes |
| `OrderId` | uuid |  |
| `PayoutId` | uuid | yes |
| `SellerId` | uuid | yes |
| `ShippedAt` | timestamp with time zone | yes |
| `ShippingShare` | numeric(18,2) | yes |
| `Status` | character varying(16) |  |
| `TrackingReference` | character varying(100) | yes |
| `UpdatedAt` | timestamp with time zone |  |

### `orders`

Entity `Order`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `CancelledBy` | character varying(16) | yes |
| `CommissionRate` | numeric(5,4) | yes |
| `CreatedAt` | timestamp with time zone |  |
| `Currency` | character varying(3) | yes |
| `DiscountTotal` | numeric(18,2) | yes |
| `FailureReason` | character varying(512) | yes |
| `Language` | text | yes |
| `PaidAt` | timestamp with time zone | yes |
| `ShippingOptionCode` | character varying(32) | yes |
| `ShippingOptionName` | character varying(100) | yes |
| `ShippingPrice` | numeric(18,2) | yes |
| `Status` | character varying(32) |  |
| `Subtotal` | numeric(18,2) | yes |
| `TaxRate` | numeric(5,4) | yes |
| `TaxTotal` | numeric(18,2) | yes |
| `TotalAmount` | numeric(18,2) |  |
| `TrackingReference` | character varying(100) | yes |
| `UpdatedAt` | timestamp with time zone |  |
| `UserId` | uuid |  |

### `parcel_returns`

Entity `ParcelReturn`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `CustomerId` | uuid |  |
| `DecidedAt` | timestamp with time zone | yes |
| `DecisionReason` | character varying(500) | yes |
| `OrderId` | uuid |  |
| `Reason` | character varying(1000) |  |
| `ReceivedAt` | timestamp with time zone | yes |
| `RefundAmount` | numeric(18,2) | yes |
| `RequestedAt` | timestamp with time zone |  |
| `SellerId` | uuid | yes |
| `SentBackAt` | timestamp with time zone | yes |
| `ShipmentId` | uuid |  |
| `Status` | character varying(16) |  |
| `TrackingReference` | character varying(100) | yes |
| `UpdatedAt` | timestamp with time zone |  |

### `payouts`

Entity `Payout`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `Amount` | numeric(18,2) |  |
| `CreatedAt` | timestamp with time zone |  |
| `Currency` | character varying(3) |  |
| `PartCount` | integer |  |
| `RecordedBy` | uuid |  |
| `SellerId` | uuid |  |

### `voucher_amounts`

Entity `VoucherAmount`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `VoucherId` | uuid |  |
| `Currency` | character varying(3) | yes |
| `FixedValue` | numeric(18,2) | yes |
| `MaxDiscount` | numeric(18,2) | yes |
| `MinSubtotal` | numeric(18,2) | yes |

### `voucher_conditions`

Entity `VoucherCondition`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `VoucherId` | uuid |  |
| `Type` | character varying(32) | yes |
| `Value` | integer | yes |

### `voucher_customer_uses`

Entity `VoucherCustomerUse`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `VoucherId` | uuid |  |
| `CustomerId` | uuid |  |
| `Uses` | integer |  |

### `voucher_redemptions`

Entity `VoucherRedemption`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `Amount` | numeric(18,2) |  |
| `Benefit` | character varying(16) |  |
| `Code` | character varying(32) |  |
| `CreatedAt` | timestamp with time zone |  |
| `Currency` | character varying(3) |  |
| `CustomerId` | uuid |  |
| `Name` | character varying(100) |  |
| `OrderId` | uuid |  |
| `ReleasedAt` | timestamp with time zone | yes |
| `SellerId` | uuid | yes |
| `VoucherId` | uuid |  |

### `voucher_targets`

Entity `VoucherTarget`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `VoucherId` | uuid |  |
| `Type` | character varying(16) | yes |
| `TargetId` | uuid |  |

### `vouchers`

Entity `Voucher`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `Benefit` | character varying(16) |  |
| `Code` | character varying(32) |  |
| `CreatedAt` | timestamp with time zone |  |
| `CreatedBy` | uuid |  |
| `EndsAt` | timestamp with time zone | yes |
| `Name` | character varying(100) |  |
| `PerCustomerLimit` | integer | yes |
| `Percent` | numeric(5,2) | yes |
| `SellerId` | uuid | yes |
| `StartsAt` | timestamp with time zone |  |
| `Status` | character varying(16) |  |
| `TotalLimit` | integer | yes |
| `UpdatedAt` | timestamp with time zone |  |
| `UsedCount` | integer |  |

## Inventory - `ecommerce_inventory_db` (3 tables)

### `returned_parcels`

Entity `ReturnedParcel`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `ReturnId` | uuid |  |
| `OrderId` | uuid |  |
| `RestockedAt` | timestamp with time zone |  |

### `stock_items`

Entity `StockItem`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `CreatedAt` | timestamp with time zone |  |
| `ProductId` | uuid |  |
| `QuantityOnHand` | integer |  |
| `QuantityReserved` | integer |  |
| `Sku` | character varying(50) |  |
| `UpdatedAt` | timestamp with time zone |  |

### `stock_reservations`

Entity `StockReservation`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `CreatedAt` | timestamp with time zone |  |
| `ExpiresAt` | timestamp with time zone |  |
| `OrderId` | uuid |  |
| `ProductId` | uuid |  |
| `Quantity` | integer |  |
| `SettledAt` | timestamp with time zone | yes |
| `SettlementReason` | character varying(512) | yes |
| `Status` | character varying(16) |  |

## Payment - `ecommerce_payment_db` (2 tables)

### `payments`

Entity `Payment`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `Amount` | numeric(18,2) |  |
| `Currency` | character varying(3) | yes |
| `FailureReason` | character varying(512) | yes |
| `OrderId` | uuid |  |
| `ProcessedAt` | timestamp with time zone |  |
| `Provider` | character varying(32) |  |
| `Status` | character varying(16) |  |
| `UserId` | uuid |  |

### `refunds`

Entity `Refund`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `Amount` | numeric(18,2) |  |
| `Currency` | character varying(3) | yes |
| `OrderId` | uuid |  |
| `PaymentId` | uuid |  |
| `Provider` | character varying(32) |  |
| `RefundedAt` | timestamp with time zone |  |
| `ReturnId` | uuid | yes |

## Orchestrator - `ecommerce_saga_db` (1 tables)

### `order_state_data`

Entity `OrderStateData`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `CorrelationId` | uuid |  |
| `CreatedAt` | timestamp with time zone |  |
| `Currency` | character varying(3) | yes |
| `CurrentState` | character varying(64) |  |
| `FailureReason` | character varying(512) | yes |
| `PaymentId` | uuid | yes |
| `TotalAmount` | numeric(18,2) |  |
| `UpdatedAt` | timestamp with time zone |  |
| `UserId` | uuid |  |

## Activity - `ecommerce_activity_db` (2 tables)

### `audit_entries`

Entity `AuditEntry`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `Action` | character varying(64) |  |
| `ActorEmail` | character varying(256) | yes |
| `ActorId` | uuid | yes |
| `ActorRole` | character varying(32) | yes |
| `After` | jsonb | yes |
| `Before` | jsonb | yes |
| `Category` | character varying(32) |  |
| `ChangeCount` | integer |  |
| `Changes` | jsonb |  |
| `OccurredAt` | timestamp with time zone |  |
| `RecordedAt` | timestamp with time zone |  |
| `Service` | character varying(32) |  |
| `SubjectId` | character varying(128) | yes |
| `SubjectType` | character varying(64) |  |
| `Summary` | character varying(1000) |  |

### `notifications`

Entity `Notification`.

| Column | Type | Null |
| :-- | :-- | :-- |
| `Id` | uuid |  |
| `CreatedAt` | timestamp with time zone |  |
| `Data` | jsonb |  |
| `Kind` | character varying(64) |  |
| `Link` | character varying(256) | yes |
| `ReadAt` | timestamp with time zone | yes |
| `RecipientId` | uuid |  |
