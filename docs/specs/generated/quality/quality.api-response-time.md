# API Response Time Quality

- **Type:** `quality`
- **Kind:** `QualityIndex`
- **ID:** `quality.api-response-time`
- **Status:** `draft`
- **Source:** `specs/quality/examples/quality.example.yaml`

## YAML

```yaml
apiVersion: jdai.upss/v1
kind: Quality
id: quality.api-response-time
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-quality-nfr-agent
  lastReviewed: 2026-03-07
  changeReason: Establish the first canonical quality specification for API response time NFRs.
category: performance
slos:
  - name: P99 API Response Time
    target: "<=500ms"
    description: 99th percentile response time for all API endpoints must remain at or below 500 milliseconds.
slis:
  - name: API Response Latency
    metric: http_request_duration_seconds
    unit: milliseconds
errorBudgets:
  - sloRef: P99 API Response Time
    budget: "0.1%"
    window: 30d
scalabilityExpectations:
  - dimension: concurrent-users
    current: "100"
    target: "1000"
trace:
  upstream:
    - specs/vision/examples/vision.example.yaml
  downstream:
    testing:
      - tests/JD.AI.Tests/Specifications/QualitySpecificationRepositoryTests.cs
    observability: []
    operations: []
```
