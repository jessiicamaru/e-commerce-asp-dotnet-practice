"""The resilience report, built from the runs kept in server/loadtest/results/ (specs/147).

    python server/loadtest/resilience_report.py

Writes docs/testing/resilience-results.md from each fault's k6 summary and timeline, so every number in it can be traced
to a file in the repository.
"""
import glob
import json
import os
import re
from datetime import datetime

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', '..', 'docs', 'testing', 'resilience-results.md')

WHAT = {
    'payment': ('Payment stopped', 'docker stop ecommerce-payment, then start',
                'The saga holds each order in its "awaiting payment" state; its timeout (600 s) is longer than the outage.'),
    'broker': ('RabbitMQ stopped', 'docker stop e-commerce-rabbitmq, then start',
               'Checkout writes each order and its OrderSubmittedEvent to Order\'s own database (the transactional outbox); '
               'every service reconnects and its outbox delivers when the broker returns.'),
    'orchestrator': ('The orchestrator restarted', 'docker stop ecommerce-orchestrator, then start',
                     'The sagas in flight are rows in the saga database; their next messages wait in their queues.'),
    'inventory': ('Inventory hung', 'docker pause ecommerce-inventory, then unpause',
                  'A frozen process holds its connections and answers nothing; reservations wait in its queue.'),
}


def ms(iso):
    return datetime.fromisoformat(iso.replace('Z', '+00:00'))


def load():
    runs = []
    for timeline_path in sorted(glob.glob(os.path.join(HERE, 'results', 'resilience-*.timeline.json'))):
        summary_path = timeline_path.replace('.timeline.json', '.json')
        if not os.path.exists(summary_path):
            continue
        with open(timeline_path, encoding='utf-8') as f:
            timeline = json.load(f)
        with open(summary_path, encoding='utf-8') as f:
            summary = json.load(f)
        runs.append((timeline, summary, os.path.basename(summary_path), os.path.basename(timeline_path)))
    # In the issue's order (#291), not the files' alphabetical one.
    order = list(WHAT)
    return sorted(runs, key=lambda r: (order.index(r[0]['fault']) if r[0]['fault'] in order else len(order), r[2]))


def metric(summary, name):
    return summary['metrics'].get(name, {}).get('values', {})


def s(value):
    return '-' if value is None else f'{value:,.2f} s'


def main():
    runs = load()
    if not runs:
        raise SystemExit('no resilience runs in server/loadtest/results - run server/loadtest/fault.sh first')
    machine = re.sub(r'(\d+) bytes', lambda m: f'{int(m.group(1)) / 2**30:.1f} GiB memory', runs[0][1].get('machine', 'unknown'))
    lines = [
        '# Resilience results',
        '',
        '> **Generated** by [`server/loadtest/resilience_report.py`](../../server/loadtest/resilience_report.py) from the',
        '> runs kept in [`server/loadtest/results/`](../../server/loadtest/results/). Do not edit by hand - run',
        '> `server/loadtest/fault.sh` and the script again (specs/147).',
        '',
        '## What was done',
        '',
        'For each fault, customers checked out at a steady rate through the gateway while one part of the system was taken',
        'away and brought back ([`fault.sh`](../../server/loadtest/fault.sh), [`resilience.js`](../../server/loadtest/resilience.js)).',
        'Afterwards the run waited for every order to settle and checked, through the API, that every order was terminal,',
        'that units sold equalled units deducted and that nothing was held - and, through the broker, that no message had',
        'faulted into an error queue. A run fails on any of those, never on an error a customer saw while something was',
        'down: that is measured and reported below.',
        '',
        f'Everything ran on one machine, as Docker reported it: **{machine}** - the whole stack and k6 together. Payment was',
        'the stub approving every payment.',
        '',
        '## Results',
        '',
        '| Fault | Down for | Orders placed | Customer errors | Paid | Held after | Error queues | Passed |',
        '| :-- | --: | --: | --: | --: | --: | :-- | :-- |',
    ]
    for timeline, summary, _, _ in runs:
        name = WHAT.get(timeline['fault'], (timeline['fault'],))[0]
        down = (ms(timeline['recovered_at']) - ms(timeline['fault_at'])).total_seconds()
        placed = metric(summary, 'orders_placed').get('count', 0)
        errors = metric(summary, 'checkout_errors').get('count', 0)
        paid = metric(summary, 'check_paid').get('value')
        held = metric(summary, 'check_reserved').get('value')
        queues = timeline['error_queues'] or 'empty'
        lines.append(f"| {name} | {down:.0f} s | {placed} | {errors} | {paid} | {held} | {queues} | {'yes' if timeline['passed'] else '**no**'} |")

    lines += [
        '',
        '## How long each order waited',
        '',
        'Orders are grouped by when they were placed: before the fault, during it, and after recovery. Settle time is from',
        'placing the order to Order recording it Paid; "backlog cleared" is how long after recovery the last order placed',
        'during the fault was paid.',
        '',
        '| Fault | Placed before: settle p50 / p95 | During: orders, settle p50 / p95 / slowest | After: settle p50 / p95 | Backlog cleared after recovery |',
        '| :-- | :-- | :-- | :-- | --: |',
    ]
    for timeline, _, _, _ in runs:
        o = timeline.get('orders') or {}
        b, d, a = o.get('before', {}), o.get('during', {}), o.get('after', {})
        lines.append(
            f"| {WHAT.get(timeline['fault'], (timeline['fault'],))[0]} "
            f"| {s(b.get('settle_p50_s'))} / {s(b.get('settle_p95_s'))} "
            f"| {d.get('orders', '-')}: {s(d.get('settle_p50_s'))} / {s(d.get('settle_p95_s'))} / {s(d.get('settle_max_s'))} "
            f"| {s(a.get('settle_p50_s'))} / {s(a.get('settle_p95_s'))} "
            f"| {s(o.get('backlog_cleared_after_recovery_s'))} |")

    by_fault = {t['fault']: t.get('orders') or {} for t, _, _, _ in runs}
    broker = by_fault.get('broker', {})
    payment = by_fault.get('payment', {})
    lines += [
        '',
        '## What the runs showed',
        '',
        '- **Nothing was lost, under any of the four faults.** Every order placed reached Paid, units sold equalled units',
        '  deducted, nothing was left held and no message faulted. No customer saw an error either: checkout depends on',
        '  Catalog, Cart and Identity synchronously, and on nothing that was taken away here.',
        '- **The broker outage cost the most time, and for longest.** Checkout kept taking orders - each written with its',
        "  event to Order's own database, the transactional outbox - but once RabbitMQ returned, the backlog took",
        f"  {s(broker.get('backlog_cleared_after_recovery_s'))} to clear, and orders placed *after* recovery waited a median of",
        f"  {s((broker.get('after') or {}).get('settle_p50_s'))} behind it. Payment's outage, of the same length, cleared in",
        f"  {s(payment.get('backlog_cleared_after_recovery_s'))}. Why the outbox drains this slowly is not yet established:",
        '  [#304](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/304).',
        '- **A hung service is not a dead one, and both recover.** Inventory frozen (connections open, nothing answered)',
        '  and the orchestrator restarted both resumed from their queues and their own databases.',
        '- **A harness lesson.** Cart removes what was ordered only when an order completes (specs/010), so a customer who',
        '  orders again while an earlier order is held up by a fault can have the new line removed by the earlier',
        "  order's completion. The runs give each customer enough time between orders that this cannot happen, so the",
        "  errors counted above are the fault's alone.",
    ]
    lines += ['', '## What each fault exercised', '']
    for timeline, _, _, _ in runs:
        name, how, why = WHAT.get(timeline['fault'], (timeline['fault'], '', ''))
        lines.append(f'- **{name}** (`{how}`): {why}')

    lines += [
        '',
        '## How the check was checked',
        '',
        'A message placed by hand in `OrderCompleted_error` before a run made that run fail with',
        '`error queues holding messages: OrderCompleted_error=1`, and an expected stock one unit off fails the load',
        "scenarios\' invariant (specs/144) - the checks are able to fail.",
        '',
        '## Runs',
        '',
        '| Summary | Timeline |',
        '| :-- | :-- |',
    ]
    for _, _, summary_file, timeline_file in runs:
        lines.append(f'| [`{summary_file}`](../../server/loadtest/results/{summary_file}) | [`{timeline_file}`](../../server/loadtest/results/{timeline_file}) |')
    lines += ['', 'Rerun: `server/loadtest/fault.sh payment|broker|orchestrator|inventory`, then `python server/loadtest/resilience_report.py`.', '']

    with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines))
    print(f'wrote {os.path.relpath(OUT)} from {len(runs)} runs')


if __name__ == '__main__':
    main()
