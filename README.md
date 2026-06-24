# ETL-Kafka-Cosmos

## Local development prerequisites

- [Podman Desktop](https://podman-desktop.io/) — container runtime
- `pip3 install podman-compose` — Docker Compose-compatible CLI for Podman
- `npm install -g azure-functions-core-tools@4` — `func` CLI for running function apps locally

## Running locally

```bash
# Start all services (Kafka, Cosmos emulator, Service Bus emulator, Azurite, SQL Server)
cd local-env
podman-compose up -d

# Run the onboarding function app
cd src/Onboarding.Function
func start

# Run the amendment function app
cd src/Amendment.Function
func start

# Tear down and wipe all data
cd local-env
podman-compose down -v
```

Cosmos DB containers (`enriched-records`, `audit-metrics`, `idempotency-records`) are created
automatically when either function app starts in the Development environment.