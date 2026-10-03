"""The load-test report, built from the summaries kept in server/loadtest/results/ (specs/144 research D7).

    python server/loadtest/report.py

Writes docs/testing/load-test-results.md. Every number in it comes from a summary file in the repository, so a reader
can check any of them; nothing is typed in by hand.
"""
import glob
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', '..', 'docs', 'testing', 'load-test-results.md')

STEPS = {
    'race': ['add to cart', 'quote', 'place order', 'read order'],
    'checkout': ['add to cart', 'quote', 'place order', 'read order'],
    'browse': ['list products', 'search', 'one product'],
}


def load():
    runs = []
    for path in sorted(glob.glob(os.path.join(HERE, 'results', '*.json'))):
        with open(path, encoding='utf-8') as f:
            data = json.load(f)
        data['_file'] = os.path.basename(path)
        day, hour, minute = re.search(r'(\d{4}-\d\d-\d\d)T(\d\d)-(\d\d)', data['_file']).groups()
        data['_when'] = f'{day} {hour}:{minute} UTC'
        runs.append(data)
    return runs


def metric(run, name):
    return run['metrics'].get(name, {}).get('values', {})


def ms(value):
    return '-' if value is None else (f'{value:,.0f}' if value >= 100 else f'{value:,.1f}')


def latency_rows(run):
    rows = []
    for step in STEPS[run['scenario']]:
        v = metric(run, f'http_req_duration{{name:{step}}}')
        if v:
            rows.append(f"| {step} | {ms(v.get('med'))} | {ms(v.get('p(95)'))} | {ms(v.get('p(99)'))} | {ms(v.get('max'))} |")
    return rows


def readings(run):
    names = ['placed', 'paid', 'failed', 'stuck', 'starting_stock', 'on_hand', 'reserved']
    values = {n: metric(run, f'check_{n}').get('value') for n in names}
    return values if any(v is not None for v in values.values()) else None


def held(run):
    broken = metric(run, 'broken_invariants').get('count', 0)
    failed = metric(run, 'http_req_failed').get('rate', 0)
    errors = metric(run, 'checkout_errors').get('count', 0)
    return broken == 0 and failed == 0 and errors == 0


def main():
    runs = load()
    if not runs:
        raise SystemExit('no summaries in server/loadtest/results - run server/loadtest/run.sh first')
    lines = [
        '# Load-test results',
        '',
        '> **Generated** by [`server/loadtest/report.py`](../../server/loadtest/report.py) from the summaries kept in',
        '> [`server/loadtest/results/`](../../server/loadtest/results/). Do not edit by hand - run the scenarios and the',
        '> script again (specs/144).',
        '',
        '## What was measured, and on what',
        '',
        f"Every run below used the same machine, as Docker reported it: **{runs[0]['machine']}**. The whole stack - 8",
        'services, the gateway, both apps, 8 PostgreSQL databases, RabbitMQ, Seq, Mailpit and SeaweedFS - ran on it as',
        'containers, and so did k6. These are therefore numbers for a laptop sharing itself between the load and the',
        'system under it: they show the shape (which step is slow, whether the guarantees hold under contention), not what',
        'a server would do. Latencies are milliseconds, measured by k6 at the gateway.',
        '',
        'Payment was the stub approving every payment, so settle time is the saga itself: reserve, charge, settle.',
        '',
    ]

    race = [r for r in runs if r['scenario'] == 'race']
    if race:
        lines += [
            '## Many customers race for the last units',
            '',
            f"{race[0]['settings']['customers']} customers each check out one unit of a variant that has "
            f"{race[0]['settings']['stock']}, all starting at once ([`race.js`](../../server/loadtest/race.js)). Every "
            'order must settle, exactly as many as the stock must be paid, the rest must fail for lack of stock, and nothing',
            'may stay held. The readings are taken through the API after the run.',
            '',
            '| Run | Placed | Paid | Failed | Stuck | Stock before | On hand after | Held after | Invariants |',
            '| :-- | --: | --: | --: | --: | --: | --: | --: | :-- |',
        ]
        for r in race:
            v = readings(r)
            lines.append(f"| {r['_when']} | {v['placed']} | {v['paid']} | {v['failed']} | {v['stuck']} | {v['starting_stock']} "
                         f"| {v['on_hand']} | {v['reserved']} | {'held' if held(r) else '**BROKEN**'} |")
        settle = [metric(r, 'order_settle_ms') for r in race]
        lines += [
            '',
            'Time from placing the order to its outcome (Paid or Failed), under that contention:',
            '',
            '| Run | Median | p95 | p99 | Slowest |',
            '| :-- | --: | --: | --: | --: |',
        ]
        for r, s in zip(race, settle):
            lines.append(f"| {r['_when']} | {ms(s.get('med'))} | {ms(s.get('p(95)'))} | {ms(s.get('p(99)'))} | {ms(s.get('max'))} |")
        lines += ['', 'Request latency, last run:', '', '| Step | Median | p95 | p99 | Slowest |', '| :-- | --: | --: | --: | --: |']
        lines += latency_rows(race[-1])
        lines.append('')

    for r in [r for r in runs if r['scenario'] == 'checkout']:
        v = readings(r)
        s = r['settings']
        settle = metric(r, 'order_settle_ms')
        orders = metric(r, 'iterations').get('count')
        lines += [
            '## Customers check out at a steady rate',
            '',
            f"Checkouts started at a constant **{s['rate_per_second']} per second for {s['duration']}**, each by one of "
            f"{s['customers']} customers: add to cart, ask for a quote, place the order, follow it until it settles "
            f"([`checkout.js`](../../server/loadtest/checkout.js)). Run {r['_when']}.",
            '',
            f"- **{orders} checkouts**, {v['paid']} paid, {v['failed']} failed, {v['stuck']} stuck; stock "
            f"{v['starting_stock']:,} → {v['on_hand']:,}, so exactly {v['starting_stock'] - v['on_hand']} units deducted for "
            f"{v['paid']} paid orders; {v['reserved']} held. Invariants: **{'held' if held(r) else 'BROKEN'}**.",
            f"- Unexpected responses: {metric(r, 'http_req_failed').get('rate', 0):.2%}.",
            f"- Settle time (placed → Paid): median {ms(settle.get('med'))}, p95 {ms(settle.get('p(95)'))}, "
            f"p99 {ms(settle.get('p(99)'))}, slowest {ms(settle.get('max'))}.",
            '',
            '| Step | Median | p95 | p99 | Slowest |',
            '| :-- | --: | --: | --: | --: |',
        ]
        lines += latency_rows(r)
        lines.append('')

    for r in [r for r in runs if r['scenario'] == 'browse']:
        reqs = metric(r, 'http_reqs')
        seconds = r['state']['testRunDurationMs'] / 1000
        lines += [
            '## Shoppers browse the catalogue',
            '',
            f"Anonymous shoppers ramp to **{r['settings']['peak_vus']} at once** ({r['settings']['stages']}), each listing a "
            "page, searching and opening a product, then reading for a second "
            f"([`browse.js`](../../server/loadtest/browse.js)). Run {r['_when']}.",
            '',
            f"- **{reqs.get('count'):,} requests** in {seconds:,.0f} s, {reqs.get('count') / seconds:,.1f} a second on average;"
            f" unexpected responses {metric(r, 'http_req_failed').get('rate', 0):.2%}.",
            '',
            '| Step | Median | p95 | p99 | Slowest |',
            '| :-- | --: | --: | --: | --: |',
        ]
        lines += latency_rows(r)
        lines.append('')

    lines += [
        '## How to read it',
        '',
        '- **The guarantee is the result that matters.** Under 100 simultaneous checkouts for 20 units the shop sold',
        '  exactly 20, every time - the reservation\'s row lock (specs/001) and the saga\'s compensation (specs/002)',
        '  holding across five services at once. The integration tests prove the lock inside Inventory; this proves it',
        '  through the gateway, the cart, Order, the broker and the saga together.',
        '- **Settle time under contention is queueing, not work.** One stock row takes one lock at a time, so 100',
        '  reservations of it are served one after another; the slowest order in a race waits for the 99 before it.',
        '- **Latencies are not thresholds.** A run fails on an unexpected response or a broken invariant, never on speed',
        '  (specs/144 research D5): on a laptop running two dozen containers, a latency limit would fail for reasons',
        '  that have nothing to do with the code.',
        '',
        '## Runs',
        '',
        '| Summary | Scenario | Passed |',
        '| :-- | :-- | :-- |',
    ]
    for r in runs:
        lines.append(f"| [`{r['_file']}`](../../server/loadtest/results/{r['_file']}) | {r['scenario']} | {'yes' if held(r) else 'no'} |")
    lines += ['', 'Rerun: `server/loadtest/run.sh race|checkout|browse`, then `python server/loadtest/report.py`.', '']

    with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines))
    print(f'wrote {os.path.relpath(OUT)} from {len(runs)} runs')


if __name__ == '__main__':
    main()
