# Quickstart: A real not-found page

## Scenario 1 - In a browser

Open `/no-such-page` in English and in Vietnamese. Expected: a heading, a sentence, a search box and "Back to the shop", all in the chosen language; searching "canon" opens `/?q=canon`.

## Scenario 2 - Tests

```bash
cd client && npx vitest run src/pages/not-found
```
