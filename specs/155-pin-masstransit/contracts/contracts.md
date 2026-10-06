# Contracts: MassTransit back on 8.3.6 after a broker outage stopped consumers for good

No HTTP, message or gRPC change. Message envelopes are MassTransit's JSON in both versions, and queue names come from
the consumers' names, which did not change.

One repository rule:

```yaml
# .github/dependabot.yml, nuget
ignore:
  - dependency-name: "MassTransit*"        # every update, patches included (amended after #357)
```
