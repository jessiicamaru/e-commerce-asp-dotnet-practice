"""Writes dashboards/ecommerce.json beside this file (specs/148).

The dashboard is generated rather than edited in Grafana: Grafana loads it read-only, and a change here is reviewed
like code. Change a panel here, then run `python server/observability/grafana/generate_dashboard.py` and commit both.
"""
import json
import os

DS = {"type": "prometheus", "uid": "prometheus"}
panels = []
pid = 0


def row(title, y):
    global pid
    pid += 1
    panels.append({"id": pid, "type": "row", "title": title, "collapsed": False,
                   "gridPos": {"h": 1, "w": 24, "x": 0, "y": y}, "panels": []})


def panel(title, kind, x, y, w, h, targets, unit=None, description=None, stack=False):
    global pid
    pid += 1
    field = {"defaults": {"unit": unit or "short", "custom": {}}, "overrides": []}
    if kind == "timeseries":
        field["defaults"]["custom"] = {"drawStyle": "line", "lineWidth": 1, "fillOpacity": 10, "showPoints": "never",
                                       "stacking": {"mode": "normal" if stack else "none"}}
    p = {
        "id": pid, "type": kind, "title": title, "datasource": DS,
        "gridPos": {"h": h, "w": w, "x": x, "y": y},
        "fieldConfig": field,
        "targets": [{"refId": chr(65 + i), "datasource": DS, "expr": e, "legendFormat": l, "range": True}
                    for i, (e, l) in enumerate(targets)],
    }
    if description:
        p["description"] = description
    if kind == "timeseries":
        p["options"] = {"legend": {"displayMode": "list", "placement": "bottom", "showLegend": True},
                        "tooltip": {"mode": "multi", "sort": "desc"}}
    if kind == "stat":
        p["options"] = {"reduceOptions": {"calcs": ["lastNotNull"], "fields": "", "values": False},
                        "colorMode": "value", "graphMode": "area", "textMode": "auto"}
    panels.append(p)


NOT_HEALTH = 'http_route!="/health"'

row("Checkout", 0)
panel("Orders paid", "stat", 0, 1, 4, 5, [('sum(ecommerce_orders{status="Paid"})', "paid")],
      description="Orders whose payment is recorded, as committed in Order's database (sampled every 15 s).")
panel("Orders failed", "stat", 4, 1, 4, 5, [('sum(ecommerce_orders{status="Failed"})', "failed")],
      description="Orders refused for stock or payment.")
panel("Orders waiting", "stat", 8, 1, 4, 5, [('sum(ecommerce_orders{status="Submitted"})', "submitted")],
      description="Placed and not yet settled: the saga is still working on them.")
panel("Orders settled per minute", "timeseries", 12, 1, 12, 5,
      [('sum by (status) (delta(ecommerce_orders{status=~"Paid|Failed"}[1m]))', "{{status}}")],
      description="How many orders reached Paid or Failed in each minute - from committed rows, so a retried consume never counts twice.")
panel("Time to settle (orders paid in the last minute)", "timeseries", 0, 6, 12, 7,
      [("ecommerce_order_settle_seconds", "p{{quantile}}")], unit="s",
      description="From placing an order to its payment, percentiles over the orders paid in the minute before each sample.")
panel("Waiting in each outbox", "timeseries", 12, 6, 12, 7,
      [("sum by (job) (ecommerce_outbox_pending_messages)", "{{job}}")],
      description="Messages written with their change and not yet delivered to the broker. Rises while RabbitMQ is down, drains when it returns.")

row("Services", 13)
panel("Requests per second", "timeseries", 0, 14, 8, 7,
      [(f"sum by (job) (rate(http_server_request_duration_seconds_count{{{NOT_HEALTH}}}[1m]))", "{{job}}")], unit="reqps",
      description="Health probes excluded.")
panel("Latency p95", "timeseries", 8, 14, 8, 7,
      [(f"histogram_quantile(0.95, sum by (job, le) (rate(http_server_request_duration_seconds_bucket{{{NOT_HEALTH}}}[1m])))", "{{job}}")], unit="s")
panel("Server errors (5xx) per second", "timeseries", 16, 14, 8, 7,
      [(f'sum by (job) (rate(http_server_request_duration_seconds_count{{{NOT_HEALTH},http_response_status_code=~"5.."}}[1m]))', "{{job}}")], unit="reqps",
      description="Health probes excluded: a /health answering 503 while the broker is down is the probe working, not a customer refused.")

row("Messaging", 21)
panel("Messages consumed per second", "timeseries", 0, 22, 12, 7,
      [("sum by (messaging_masstransit_message_type) (rate(messaging_masstransit_consume_ea_total[1m]))", "{{messaging_masstransit_message_type}}")],
      unit="ops", stack=True)
panel("Consume failures per second (retried or faulted)", "timeseries", 12, 22, 12, 7,
      [("sum by (job, messaging_masstransit_exception_type) (rate(messaging_masstransit_consume_errors_ea_total[1m]))",
        "{{job}}: {{messaging_masstransit_exception_type}}")], unit="ops",
      description="Every failed attempt, including the serialization conflicts the transient retry absorbs (specs/145). A message lost is one in an _error queue - see the next panel.")
panel("Messages waiting in RabbitMQ, by queue", "timeseries", 0, 29, 12, 7,
      [("topk(10, sum by (queue) (rabbitmq_detailed_queue_messages))", "{{queue}}")],
      description="Ready and unacknowledged. An _error queue above zero is a message that faulted.")
panel("Consume duration p95", "timeseries", 12, 29, 12, 7,
      [("histogram_quantile(0.95, sum by (job, le) (rate(messaging_masstransit_consume_duration_milliseconds_bucket[1m])))", "{{job}}")], unit="ms")

row("Runtime", 36)
panel("Memory (working set)", "timeseries", 0, 37, 8, 7,
      [("sum by (job) (dotnet_process_memory_working_set_bytes)", "{{job}}")], unit="bytes")
panel("Database connections", "timeseries", 8, 37, 8, 7,
      [("sum by (job) (db_client_connection_count)", "{{job}}")],
      description="Npgsql's pool, per service: idle and in use.")
panel("GC pause time", "timeseries", 16, 37, 8, 7,
      [("sum by (job) (rate(dotnet_gc_pause_time_seconds_total[1m]))", "{{job}}")], unit="percentunit",
      description="Fraction of each second spent paused for garbage collection.")

dashboard = {
    "uid": "ecommerce-overview",
    "title": "E-commerce overview",
    "description": "Checkout, services, messaging and runtime (specs/148). Generated by server/observability/grafana/generate_dashboard.py - change it there, not here.",
    "tags": ["ecommerce"],
    "timezone": "browser",
    "schemaVersion": 41,
    "version": 1,
    "editable": False,
    "refresh": "15s",
    "time": {"from": "now-30m", "to": "now"},
    "panels": panels,
    "templating": {"list": []},
    "annotations": {"list": []},
}
with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "dashboards", "ecommerce.json"), "w", encoding="utf-8", newline="\n") as f:
    json.dump(dashboard, f, indent=2)
    f.write("\n")
print(f"{len(panels)} panels")
