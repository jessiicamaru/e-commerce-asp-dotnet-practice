# RabbitMQ Setup & Monitoring Guide

This guide explains how to spin up and monitor **RabbitMQ** using Docker Compose. RabbitMQ is used as the message broker for background message processing (e.g., sending emails, payment processing).

---

## 1. Port Explanations

RabbitMQ runs on two primary ports:
* **`5672`**: The standard AMQP port. This is used by your C#/.NET application (via libraries like MassTransit or RabbitMQ.Client) to publish and consume messages.
* **`15672`**: The HTTP Management Console port. This is used by developers and administrators to monitor and configure the broker via a web browser.

---

## 2. Docker Compose Configuration

To configure RabbitMQ, we use the official **`rabbitmq:3-management-alpine`** image, which includes the web management plugin out-of-the-box and has a small footprint.

### 2.1 Update `.env` & `.env.example`
Add RabbitMQ credentials to your local [**`.env`**](file:///d:/Code/CSharp/e-commerce/server/.env) file:

```env
# RabbitMQ Configuration
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest
```

### 2.2 Update `docker-compose.yml`
Add the `rabbitmq` service under the `services` section in [`docker-compose.yml`](file:///d:/Code/CSharp/e-commerce/server/docker-compose.yml):

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
    restart: unless-stopped

volumes:
  postgres_data:
  rabbitmq_data:        # Add this volume for data persistence
```

---

## 3. How to Start RabbitMQ

To build and start your RabbitMQ container, run:

```bash
docker compose up -d
```

This will download the image, create the `rabbitmq_data` volume, and start the container in detached (background) mode.

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
