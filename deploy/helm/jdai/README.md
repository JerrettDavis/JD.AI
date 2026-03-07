# JD.AI Helm Chart

Deploys:

- `JD.AI.Gateway` Deployment + Service
- `JD.AI.Daemon` Deployment
- optional Ingress
- optional HPA and PodDisruptionBudget for gateway
- optional ConfigMap/Secret wiring

## Install

```bash
helm upgrade --install jdai ./deploy/helm/jdai \
  --namespace jdai \
  --create-namespace
```

## Key values

- `gateway.image.repository` / `gateway.image.tag`
- `daemon.image.repository` / `daemon.image.tag`
- `gateway.hpa.*`
- `gateway.pdb.*`
- `gateway.persistence.*` / `daemon.persistence.*`
- `ingress.*`
- `config.data` / `config.secretData`

## Service mesh compatibility

Use pod annotations to enable sidecar injection:

```yaml
gateway:
  podAnnotations:
    sidecar.istio.io/inject: "true"
```
