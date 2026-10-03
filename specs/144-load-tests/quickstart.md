# Quickstart: Load-test checkout and measure it

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d     # the stub approving
./loadtest/run.sh race        # 100 customers, 20 units
./loadtest/run.sh checkout    # a steady arrival rate of checkouts
./loadtest/run.sh browse      # anonymous reads, ramping
python loadtest/report.py     # docs/testing/load-test-results.md from the kept summaries
```

## Expected results

- **race**:
  - `paid=20 failed=80 submitted=0 on_hand=0 reserved=0`;
  - every threshold green, exit 0.
- **checkout**:
  - no unexpected status;
  - every order settled;
  - paid quantity equals the stock deducted;
  - nothing held.
- **browse**: no unexpected status.

## A negative control

```bash
./loadtest/run.sh race -e EXPECTED_STOCK=21
```

Expected: the invariant counter is above 0, the threshold fails, and the exit code is not 0. The check is doing work.
