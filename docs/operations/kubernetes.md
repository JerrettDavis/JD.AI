---
title: Kubernetes Deployment
description: "Deploy JD.AI Gateway and Daemon to Kubernetes with Helm, autoscaling, disruption budgets, health probes, and SBOM-backed container images."
---

# Kubernetes Deployment

JD.AI ships container and Helm assets for cloud-native deployment:

- Multi-stage Dockerfiles for `JD.AI.Gateway`, `JD.AI.Daemon`, and `JD.AI` (TUI)
- Helm chart at `deploy/helm/jdai`
- CI image publishing to GitHub Container Registry (GHCR)
- SBOM generation + attestation for every published container image

## Container images

Published images:

- `ghcr.io/jerrettdavis/jd.ai-gateway`
- `ghcr.io/jerrettdavis/jd.ai-daemon`
- `ghcr.io/jerrettdavis/jd.ai-tui`

The workflow `.github/workflows/containers.yml` builds and pushes images on `main` and `v*` tags.

## Helm quick start

```bash
kubectl create namespace jdai

helm upgrade --install jdai ./deploy/helm/jdai \
  --namespace jdai
```

## Common overrides

```yaml
gateway:
  image:
    repository: ghcr.io/jerrettdavis/jd.ai-gateway
    tag: v1.0.0
  hpa:
    enabled: true
    minReplicas: 2
    maxReplicas: 8
  pdb:
    enabled: true
    minAvailable: 1

daemon:
  image:
    repository: ghcr.io/jerrettdavis/jd.ai-daemon
    tag: v1.0.0

ingress:
  enabled: true
  className: nginx
  hosts:
    - host: jdai.example.com
      paths:
        - path: /
          pathType: Prefix
```

Apply:

```bash
helm upgrade --install jdai ./deploy/helm/jdai \
  --namespace jdai \
  -f values-prod.yaml
```

## Probes and availability

Gateway probes default to:

- startup: `GET /health/live`
- readiness: `GET /health/ready`
- liveness: `GET /health/live`

Daemon probes default to:

- startup/readiness/liveness: `GET /health`

The chart also includes:

- `HorizontalPodAutoscaler` for gateway
- `PodDisruptionBudget` for gateway

## Config and secrets

Use chart values:

- `config.data` -> ConfigMap
- `config.secretData` -> Secret

Both are injected as `envFrom` into gateway and daemon pods.

## Service mesh compatibility

Use pod annotations for sidecar injection or mesh-specific settings:

```yaml
gateway:
  podAnnotations:
    sidecar.istio.io/inject: "true"

daemon:
  podAnnotations:
    sidecar.istio.io/inject: "true"
```

No host networking or fixed node ports are required by the chart.

## Local image builds

```bash
docker build -f deploy/docker/Dockerfile.gateway -t jdai-gateway:local .
docker build -f deploy/docker/Dockerfile.daemon -t jdai-daemon:local .
docker build -f deploy/docker/Dockerfile.tui -t jdai-tui:local .
```

## Observability / OTLP

Enable OTLP export by setting `telemetry.enabled = true`:

```yaml
telemetry:
  enabled: true
  otlpEndpoint: http://otel-collector:4317
  otlpProtocol: grpc
  serviceName: jdai
```

The chart injects `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_PROTOCOL`, and `OTEL_SERVICE_NAME` as environment variables into all pods.

## Distributed workflow workers

Enable the distributed workflow worker pool:

```yaml
distributedWorker:
  enabled: true
  replicaCount: 3
  transport: redis
  redis:
    connectionString: redis-master:6379
    streamKey: jdai:workflows
    consumerGroup: jdai-workers
```

Workers are stateless — replicas compete for messages via the consumer group. Scale horizontally by increasing `replicaCount`.

## Batch/job mode

Run a single agent workflow as a Kubernetes Job (exits 0/1 for CI/CD integration):

```yaml
batchJob:
  enabled: true
  agent: code-reviewer
  workflow: pr-review
```

Or via `kubectl`:

```bash
kubectl create job pr-review \
  --image=ghcr.io/jerrettdavis/jd.ai-daemon:latest \
  -- dotnet JD.AI.Daemon.dll run --agent code-reviewer --workflow pr-review
```

## Kubernetes Operator (v2 — planned)

A CRD-based operator for managing `JdAiAgent` custom resources is planned for v2.
See [Kubernetes Operator Design](../architecture/kubernetes-operator-design.md) for the full design.

## Root Dockerfile

The repo root includes a `Dockerfile` that builds `JD.AI.Daemon` and is compatible with standard `docker build .` usage:

```bash
docker build -t jdai:local .
```

## See also

- [Service Deployment](deployment.md)
- [Observability](observability.md)
- [Security](security.md)
