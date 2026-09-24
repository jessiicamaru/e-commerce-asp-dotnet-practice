# Gateway routes

> **Generated** by [`docs/tools/generate_reference.py`](../tools/generate_reference.py) from commit `c42d85b`. Do not edit by hand - change the code and run the script again.

What the YARP gateway on `:5000` forwards, and where. The storefront and Bruno talk only to the gateway. A new endpoint under a new path prefix needs a route here, or it is a 404 that looks like a missing feature. Container destinations are overridden by command-line arguments in `docker-compose.app.yml`; the addresses below are the local-development ones.

| Route | Path | Cluster | Destination | Rewrites to |
| :-- | :-- | :-- | :-- | :-- |
| `activity-health-route` | `/api/activity/health` | `activity-cluster` | http://localhost:5063/ | `/health` |
| `audit-root-route` | `/api/audit` | `activity-cluster` | http://localhost:5063/ |  |
| `audit-route` | `/api/audit/{**catch-all}` | `activity-cluster` | http://localhost:5063/ |  |
| `notifications-root-route` | `/api/notifications` | `activity-cluster` | http://localhost:5063/ |  |
| `notifications-route` | `/api/notifications/{**catch-all}` | `activity-cluster` | http://localhost:5063/ |  |
| `cart-root-route` | `/api/cart` | `cart-cluster` | http://localhost:5062/ |  |
| `cart-health-route` | `/api/cart/health` | `cart-cluster` | http://localhost:5062/ | `/health` |
| `cart-route` | `/api/cart/{**catch-all}` | `cart-cluster` | http://localhost:5062/ |  |
| `catalog-health-route` | `/api/catalog/health` | `catalog-cluster` | http://localhost:5057/ | `/health` |
| `catalog-categories-route` | `/api/categories/{**catch-all}` | `catalog-cluster` | http://localhost:5057/ |  |
| `catalog-products-route` | `/api/products/{**catch-all}` | `catalog-cluster` | http://localhost:5057/ |  |
| `catalog-reviews-root-route` | `/api/reviews` | `catalog-cluster` | http://localhost:5057/ |  |
| `catalog-reviews-route` | `/api/reviews/{**catch-all}` | `catalog-cluster` | http://localhost:5057/ |  |
| `addresses-root-route` | `/api/addresses` | `identity-cluster` | http://localhost:5056/ |  |
| `addresses-route` | `/api/addresses/{**catch-all}` | `identity-cluster` | http://localhost:5056/ |  |
| `identity-route` | `/api/auth/{**catch-all}` | `identity-cluster` | http://localhost:5056/ |  |
| `identity-health-route` | `/api/identity/health` | `identity-cluster` | http://localhost:5056/ | `/health` |
| `sellers-root-route` | `/api/sellers` | `identity-cluster` | http://localhost:5056/ |  |
| `sellers-route` | `/api/sellers/{**catch-all}` | `identity-cluster` | http://localhost:5056/ |  |
| `shop-applications-root-route` | `/api/shop-applications` | `identity-cluster` | http://localhost:5056/ |  |
| `shop-applications-route` | `/api/shop-applications/{**catch-all}` | `identity-cluster` | http://localhost:5056/ |  |
| `users-root-route` | `/api/users` | `identity-cluster` | http://localhost:5056/ |  |
| `users-route` | `/api/users/{**catch-all}` | `identity-cluster` | http://localhost:5056/ |  |
| `inventory-health-route` | `/api/inventory/health` | `inventory-cluster` | http://localhost:5060/ | `/health` |
| `inventory-reservations-route` | `/api/reservations/{**catch-all}` | `inventory-cluster` | http://localhost:5060/ |  |
| `inventory-stock-route` | `/api/stock/{**catch-all}` | `inventory-cluster` | http://localhost:5060/ |  |
| `order-health-route` | `/api/order/health` | `order-cluster` | http://localhost:5059/ | `/health` |
| `order-route` | `/api/orders/{**catch-all}` | `order-cluster` | http://localhost:5059/ |  |
| `payment-health-route` | `/api/payment/health` | `payment-cluster` | http://localhost:5061/ | `/health` |
| `payment-route` | `/api/payments/{**catch-all}` | `payment-cluster` | http://localhost:5061/ |  |
