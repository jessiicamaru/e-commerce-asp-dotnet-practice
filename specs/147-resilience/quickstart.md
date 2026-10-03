# Quickstart: Checkout survives a service or the broker going down

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d   # the stub approving
./loadtest/fault.sh payment
./loadtest/fault.sh broker
./loadtest/fault.sh orchestrator
./loadtest/fault.sh inventory
python loadtest/resilience_report.py     # docs/testing/resilience-results.md
```

Expected for each:
- exit 0;
- every order Paid, units sold equal units deducted, nothing held;
- no `_error` queue holding a message;
- the timeline file beside the summary.

## A negative control

Publish a message to an error queue by hand before a run (or set `EXPECTED_STOCK` one off). Expected: the run fails
and names it.
