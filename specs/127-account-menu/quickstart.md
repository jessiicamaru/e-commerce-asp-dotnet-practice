# Quickstart: One account menu everywhere

## Scenario 1 - In a browser

Signed in, open the avatar menu, the phone menu (390px) and `/account`. Expected: the same entries in the same order.

## Scenario 2 - Tests

```bash
cd client && npx vitest run src/constants/account src/components/layout src/pages/account
```
