# Storefront

A deliberately thin web client for this backend (issue [#23](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/23)).
Its job is to exercise the API the way a person would and to surface what the backend lacks — not to
be polished. Built one sub-issue at a time (#34–#39).

React 19 + Vite + TypeScript. It talks **only to the gateway**, under `/api`.

## Run it

```bash
# the backend first - everything behind the gateway on :5000
cd server && docker compose -f docker-compose.yml -f docker-compose.app.yml up -d

cd client
npm install
npm run dev          # http://localhost:5173 ; /api is proxied to http://localhost:5000
```

Point the proxy elsewhere with `GATEWAY_URL=http://host:port npm run dev`.

## Check it

```bash
npm run lint         # oxlint
npm run build        # tsc -b, then vite build - what CI runs
```

## How it is put together

- `src/api/http.ts` — the only way to call the backend. Turns every ProblemDetails error into an
  `ApiError` with the status and per-field messages, attaches the access token, and retries once
  after a silent refresh on 401.
- **The access token lives in memory only.** The refresh token is Identity's HttpOnly cookie, which
  JavaScript never sees; the Vite proxy keeps the browser on one origin so that cookie just works.
- `src/pages/` — one file per page.
