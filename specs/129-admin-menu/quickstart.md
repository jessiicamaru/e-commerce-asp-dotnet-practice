# Quickstart: The admin console's menu is grouped and shows what is waiting

## Scenario 1 - In a browser

As the administrator, open `/admin`: grouped links, counts beside Orders to ship, Products to review, Shop applications, Reports, Returns. Find an order, open one: Find an order stays highlighted and the breadcrumb returns to the search.

## Scenario 2 - Tests

```bash
cd client && npx vitest run src/layouts/admin-layout src/pages/admin-order
```
