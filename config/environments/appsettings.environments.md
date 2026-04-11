# Environment Configuration Guide — DARAH ECM

## Strategy
- Development: appsettings.Development.json (committed, no real secrets)
- Test:        appsettings.Test.json (committed, test DB only)
- Production:  Environment variables ONLY — never committed to repo

## Required Production Environment Variables

| Variable | Example Value | Notes |
|----------|--------------|-------|
| DB_CONNECTION_STRING | Server=sqlserver;Database=DarahECM;User Id=ecm_app;Password=XXX | Use dedicated app user, not SA |
| SQL_SA_PASSWORD | STRONG_SA_PASS | SQL Server admin password |
| JWT_SECRET_KEY | 32+ char random string | openssl rand -hex 32 |
| REDIS_PASSWORD | STRONG_REDIS_PASS | |
| SMTP_HOST | smtp.darah.gov.sa | |
| SMTP_PORT | 587 | |
| SMTP_USERNAME | ecm-noreply@darah.gov.sa | |
| SMTP_PASSWORD | SMTP_PASS | |
| GRAFANA_PASSWORD | GRAFANA_ADMIN_PASS | |
| SAP_BASE_URL | https://sap-gw.darah.gov.sa | Sprint 6 |
| SAP_CLIENT_ID | SAP_CLIENT_ID | Sprint 6 |
| SAP_CLIENT_SECRET | SAP_CLIENT_SECRET | Sprint 6, use secrets manager |
| SF_INSTANCE_URL | https://darah.my.salesforce.com | Sprint 6 |
| SF_CLIENT_ID | SF_CLIENT_ID | Sprint 6 |
| SF_CLIENT_SECRET | SF_CLIENT_SECRET | Sprint 6, use secrets manager |

## appsettings.json (base — committed)
All defaults. No secrets. Production overrides via environment variables.

## Serilog Production Template
```
{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{CorrelationId}] [{UserId}] {Message:lj}{NewLine}{Exception}
```

## Health Check Endpoints
- /health       → simple liveness
- /health/ready → readiness (DB + Redis connected)
- /health/live  → liveness (process alive)
- /metrics      → Prometheus metrics (internal only)
