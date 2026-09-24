# RabbitMQ Setup & Monitoring Guide

This guide explains how to spin up and monitor **RabbitMQ** using Docker Compose. RabbitMQ carries every asynchronous message in the system - 20 message types, listed with their publishers and consumers in [reference/messages.md](../reference/messages.md):

* the checkout saga's commands and events (Order, Orchestrator, Inventory, Payment, Cart);
* what happens after the saga: `OrderCancelledEvent` to Inventory and Payment, `ParcelDeliveredEvent` to Catalog;
* read-model feeds: Catalog's product events to Inventory, Inventory's stock availability to Catalog, Identity's seller events to Catalog;
* `AuditEntryRecorded` and `UserNotificationRequested` from Identity, Catalog, Order, Inventory and Payment to Activity.

Eight of the nine processes connect to it; the gateway does not. Every publish goes through the publisher's transactional outbox ([reliable messaging](../architecture/reliable-messaging-and-outbox-pattern.md)).

---

## 1. Port Explanations

RabbitMQ runs on two primary ports:
* **`5672`**: The standard AMQP port. This is used by your C#/.NET application (via libraries like MassTransit or RabbitMQ.Client) to publish and consume messages.
* **`15672`**: The HTTP Management Console port. This is used by developers and administrators to monitor and configure the broker via a web browser.

---

## 2. Docker Compose Configuration

To configure RabbitMQ, we use the official **`rabbitmq:3-management-alpine`** image, which includes the web management plugin out-of-the-box and has a small footprint.

### 2.1 Update `.env` & `.env.example`
Add RabbitMQ credentials to your local [**`.env`**](../../server/.env) file:

```env
# RabbitMQ Configuration
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest
```

### 2.2 Update `docker-compose.yml`
Add the `rabbitmq` service under the `services` section in [`docker-compose.yml`](../../server/docker-compose.yml):

```yaml
  rabbitmq:
    image: rabbitmq:3-management-alpine
    container_name: e-commerce-rabbitmq
    environment:
      - RABBITMQ_DEFAULT_USER=${RABBITMQ_USER}
      - RABBITMQ_DEFAULT_PASS=${RABBITMQ_PASSWORD}
    ports:
      - "5672:5672"     # AMQP Broker port
      - "15672:15672"   # Management Web UI port
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "-q", "ping"]
      interval: 10s
      timeout: 5s
      retries: 12
    restart: unless-stopped

volumes:
  # ...one postgres_<service>_data volume per database...
  rabbitmq_data:        # the broker's queues and messages survive a restart
```

---

## 3. How to Start RabbitMQ

To build and start your RabbitMQ container, run:

```bash
docker compose up -d
```

This will download the image, create the `rabbitmq_data` volume, and start the container in detached (background) mode.

> **Two variable names, one password.** The services read **`RABBITMQ_PASS`**; this compose file and
> `.env.example` use **`RABBITMQ_PASSWORD`**. A non-default password needs *both* set to the same
> value, or the services fall back to `guest` and fail to connect against a broker that no longer
> accepts it.
>
> **In containers**, `RABBITMQ_HOST` is `rabbitmq` — the compose service name — not `localhost`.
> The host path is unchanged. See [Running in Containers](./running-in-containers.md).
>
> The broker has a health check as of 2026-09-17, so `docker compose ps` reports whether it is
> actually ready rather than merely running. Services wait on it with
> `depends_on: condition: service_healthy`.

---

## 4. How to Monitor RabbitMQ (Management UI)

Once the container is running:

1. Open your web browser and navigate to: **[http://localhost:15672](http://localhost:15672)**
2. Log in using your configured credentials (default: Username: `guest`, Password: `guest`).

### Key Monitoring Areas:
* **Overview**: Real-time graph showing message rates (publishing, delivering) and total queues state.
* **Connections**: Details of active clients (IPs, protocol version, state) connected to port `5672`.
* **Exchanges**: List of message routers. You can view bindings and publish test messages here.
* **Queues**: List of all queues, message counts (ready, unacknowledged), memory usage, and consumers. You can inspect queues and read messages directly from this panel for debugging.

### 4.1 What the queues are called, and what to look for

MassTransit creates the exchanges and queues itself at startup; nothing is declared by hand. A queue
is named after the **consumer class**, with a prefix in the services that set one: `IdentitySvc`,
`CatalogSvc`, `CartSvc`, `OrderSvc` and `ActivitySvc`. Inventory, Payment and the Orchestrator use
no prefix, which is why their consumers of the same event are named for what they do
(`RestockCancelledOrderConsumer`, `RefundCancelledOrderConsumer`).

```bash
docker exec e-commerce-rabbitmq rabbitmqctl list_queues name messages consumers
```

* **A queue with two consumers** where each service should have its own is two services sharing one
  queue because their consumer classes have the same name - each message then reaches only one of
  them. This is how `OrderCompletedConsumer` in Inventory and Order once competed for completions.
* **A queue ending in `_error`** holds messages whose consumer threw. No retry policy is configured,
  so a consumer that throws faults on its first attempt, and nothing replays the message. It stays
  there until somebody looks.
* **Messages piling up with zero consumers** means the consuming service is not running.

After wiping `rabbitmq_data` nothing needs to be recreated by hand: the services declare their
topology again when they reconnect. Messages that were still in the old broker are lost; messages
still in a publisher's outbox are delivered once the broker is back.
