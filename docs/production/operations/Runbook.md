# DARAH ECM — Production Runbook
# كتيب التشغيل الإنتاجي | Version: 1.0 | April 2026

---

## PART 1: OPERATIONAL MODEL

### Support Tiers
| Level | Team | Response Time | Examples |
|-------|------|---------------|---------|
| L1 — User Support | Help Desk | < 4 hours business | Can't login, notification not received, UI question |
| L2 — Application | ECM Operations | < 2 hours business | Workflow stuck, document inaccessible, sync failed |
| L3 — Platform | IT Infrastructure | < 1 hour (critical: 30min) | DB failure, container down, network issue |
| L4 — Development | ECM Dev Team | < 4 hours (critical: 1hr) | Bug fix, data correction, emergency patch |

### Escalation Path
```
User Issue → L1 Help Desk
              ↓ (not resolved in 4hrs or severity Medium+)
           L2 ECM Operations
              ↓ (not resolved in 2hrs or severity High+)
           L3 IT Infrastructure + L4 Dev Team (parallel)
              ↓ (Critical — system down)
           Emergency CAB → All hands + Management notification
```

---

## PART 2: INCIDENT MANAGEMENT

### Severity Classification
| Severity | Definition | Example | SLA Response | SLA Resolution |
|----------|-----------|---------|-------------|----------------|
| P1 — Critical | System inaccessible, data loss risk | API down, DB unreachable | 15 minutes | 2 hours |
| P2 — High | Core feature broken, major business impact | Workflow stuck, login failing | 30 minutes | 4 hours |
| P3 — Medium | Degraded functionality, workaround exists | Sync failing, notifications delayed | 2 hours | 1 business day |
| P4 — Low | Minor issue, no business impact | UI cosmetic, help text wrong | 1 business day | 1 week |

### Incident Response Procedure (P1/P2)
```bash
# Step 1: Verify the incident
curl -f https://ecm.darah.gov.sa/health/ready
docker ps --filter name=ecm --format "table {{.Names}}\t{{.Status}}"

# Step 2: Check recent logs
docker logs ecm-api-blue --tail 100 --since 30m 2>&1 | grep -E "ERROR|CRITICAL|Exception"

# Step 3: Check metrics
# Open Grafana: http://ecm-grafana:3000
# Dashboard: ECM Production Overview

# Step 4: Check database
docker exec ecm-sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P "$SQL_SA_PASSWORD" \
  -Q "SELECT @@VERSION; SELECT COUNT(*) FROM Documents;"

# Step 5: Communicate
# - Notify stakeholders via official channel
# - Create incident ticket (if P1/P2, do NOT skip this)
# - Update status page every 30 minutes during incident
```

### Rollback Procedure (Blue-Green)
```bash
# Current state: Blue is active, Green is new failed deployment

# Step 1: Verify Blue is healthy
curl https://ecm.darah.gov.sa/health/ready

# Step 2: Update nginx to point back to Blue only
sed -i 's/# server api-blue/server api-blue/' /deploy/production/nginx/nginx.conf
sed -i 's/server api-green/# server api-green/' /deploy/production/nginx/nginx.conf
docker exec ecm-nginx nginx -s reload

# Step 3: Stop failed Green
docker-compose -f docker-compose.prod.yml --profile green stop api-green

# Step 4: Verify Blue serving traffic
watch -n 5 'curl -s https://ecm.darah.gov.sa/health | python3 -m json.tool'

# Step 5: Notify team and log incident
echo "Rollback complete at $(date -u)" >> /data/ecm/logs/deployments.log
```

---

## PART 3: BACKUP & DISASTER RECOVERY

### Backup Strategy (RPO = 1 hour, RTO = 4 hours)

| Component | Backup Type | Frequency | Retention | Location |
|-----------|-------------|-----------|-----------|----------|
| SQL Server (full) | Full backup | Daily 02:00 | 30 days | /data/ecm/backup + offsite NAS |
| SQL Server (diff) | Differential | Every 4hrs | 7 days | /data/ecm/backup |
| SQL Server (log) | Transaction log | Every 15min | 3 days | /data/ecm/backup |
| File Storage | Rsync | Hourly | 90 days | Offsite NAS |
| Redis AOF | Append-only file | Continuous | 7 days | Docker volume + backup |
| Configuration | Git | On every change | Indefinite | GitHub (private) |

### SQL Server Backup Scripts
```bash
#!/bin/bash
# /deploy/scripts/backup-sqlserver.sh
# Run via cron: 0 2 * * * /deploy/scripts/backup-sqlserver.sh

DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR="/data/ecm/backup/sql"
DB_NAME="DarahECM"

mkdir -p $BACKUP_DIR

docker exec ecm-sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P "$SQL_SA_PASSWORD" \
  -Q "BACKUP DATABASE [$DB_NAME]
      TO DISK = '/var/opt/mssql/backup/${DB_NAME}_FULL_${DATE}.bak'
      WITH COMPRESSION, STATS = 10, CHECKSUM;"

echo "Backup completed: ${DB_NAME}_FULL_${DATE}.bak at $(date)"

# Sync to offsite NAS
rsync -az --delete $BACKUP_DIR/ nas-server:/ecm/backup/sql/

# Remove local backups older than 7 days
find $BACKUP_DIR -name "*.bak" -mtime +7 -delete
```

### Restore Procedure
```bash
#!/bin/bash
# /deploy/scripts/restore-sqlserver.sh
# Usage: ./restore-sqlserver.sh BACKUP_FILE_PATH

BACKUP_FILE=$1
DB_NAME="DarahECM"

if [ -z "$BACKUP_FILE" ]; then
  echo "Usage: $0 /path/to/backup.bak"
  exit 1
fi

echo "WARNING: This will restore $DB_NAME from $BACKUP_FILE"
read -p "Type 'CONFIRM' to proceed: " confirm
[ "$confirm" != "CONFIRM" ] && exit 1

# Stop API to prevent writes during restore
docker stop ecm-api-blue ecm-api-green ecm-hangfire

docker exec ecm-sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P "$SQL_SA_PASSWORD" \
  -Q "ALTER DATABASE [$DB_NAME] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
      RESTORE DATABASE [$DB_NAME]
      FROM DISK = '$BACKUP_FILE'
      WITH REPLACE, STATS = 10;
      ALTER DATABASE [$DB_NAME] SET MULTI_USER;"

# Restart API
docker start ecm-api-blue ecm-hangfire

echo "Restore complete. Verify application health."
curl https://ecm.darah.gov.sa/health/ready
```

### Disaster Recovery Targets
| Scenario | RPO | RTO | Procedure |
|----------|-----|-----|-----------|
| Single API container failure | 0 (stateless) | < 5 min | Docker auto-restart |
| Redis failure | 0 (DB fallback) | < 10 min | Restart Redis, cache warms automatically |
| SQL Server failure | 15 min (log backup interval) | < 2 hours | Restore from last log backup |
| Storage failure | 1 hour (rsync interval) | < 4 hours | Restore from NAS sync |
| Full DC failure | 1 hour | < 8 hours | Provision new infra + restore from offsite |

---

## PART 4: CHANGE MANAGEMENT (CAB)

### Change Categories
| Category | Definition | Approval Required | Examples |
|----------|-----------|-------------------|---------|
| Standard | Pre-approved, low-risk | None (checklist) | Configuration updates, minor patches |
| Normal | Planned, medium-risk | CAB weekly meeting | New features, DB schema changes |
| Emergency | Urgent, high-risk | Emergency CAB | Critical security patch, P1 fix |

### Deployment Checklist (Normal Change)
```
Pre-Deployment:
  ☐ Change request created and approved by CAB
  ☐ Rollback plan documented
  ☐ UAT passed in TEST environment
  ☐ Database backup taken (within 1 hour of deployment)
  ☐ All containers healthy before start
  ☐ Stakeholders notified (maintenance window)

During Deployment:
  ☐ Green slot started with new image
  ☐ Green slot health check passed
  ☐ Smoke test on Green slot (5 key endpoints)
  ☐ Nginx switched to Green
  ☐ Monitoring for 15 minutes
  ☐ Blue slot kept on standby for 30 minutes

Post-Deployment:
  ☐ All health checks green
  ☐ Error rate back to baseline (< 1%)
  ☐ No new alerts firing
  ☐ Change request closed with result
  ☐ Blue slot decommissioned (after 24hrs)
```

---

## PART 5: DATA GOVERNANCE

### Data Ownership
| Data Domain | Owner | Custodian | Review Frequency |
|-------------|-------|-----------|-----------------|
| Documents | Department heads | ECM Operations | Quarterly |
| Records | Records Manager | IT Operations | Annually |
| Audit Logs | Compliance Officer | IT Security | Quarterly |
| Workspace Metadata | Business owners | ECM Operations | Per workspace lifecycle |
| External Sync Data | IT Integration | ECM Operations | Monthly |

### Retention Policy Enforcement
```
Monthly (first Monday):
  1. Run retention report: GET /api/v1/audit/summary (filter: retention events)
  2. Identify documents past RetentionExpiresAt with no legal hold
  3. Notify data owners via automated email
  4. Create disposal requests (requires Records Manager approval)
  5. Log disposal approvals and executions

Quarterly:
  1. Review and update retention policies with Records Manager
  2. Validate all document types have assigned retention policies
  3. Review legal hold list — confirm holds still required
  4. Audit classification levels — reclassify if business context changed
```

### Audit Review Procedures
```
Weekly:
  GET /api/v1/audit/logs?severity=Critical,Error&dateFrom=last7days
  Review: Failed authentications, access denials, system errors

Monthly:
  GET /api/v1/audit/export?format=Excel&dateFrom=lastMonth
  Review: All document lifecycle events, workflow decisions
  Report: Summary to Compliance Officer

Annually:
  Full audit review by Compliance + Records Manager + Legal
  Report: All legal hold events, disposal events, classification changes
  Archive: Monthly audit exports to long-term storage (7 years)
```

---

## PART 6: GO-LIVE STRATEGY

### Phased Rollout Plan

**Phase 1 — Pilot (Week 1-2)**
- Pilot department: IT Department (20 users)
- Scope: Document upload, workflow, basic search
- Monitoring: Intensive (check hourly)
- Rollback trigger: Error rate > 5% OR P1 incident

**Phase 2 — Expansion (Week 3-4)**
- Add: Finance + Legal departments (50 additional users)
- Enable: Records management, legal hold
- Monitoring: Normal
- Rollback trigger: P1 incident not resolved within RTO

**Phase 3 — Full Go-Live (Week 5)**
- All departments (remaining 200+ users)
- Enable: External SAP/Salesforce integration
- Hypercare: 2-week intensive support period
- Rollback trigger: Only on system-wide failure

### Hypercare Period (Weeks 5-7)
- Daily 9:00 AM status call between ECM team + key stakeholders
- L2 response time reduced to 1 hour
- All P3+ issues tracked daily
- Weekly report to management
- L1 Help Desk staffed until 8:00 PM

### Go-Live Day Checklist
```
D-7 (1 week before):
  ☐ Production environment fully validated
  ☐ All UAT scenarios passed
  ☐ Security testing cleared
  ☐ Backup strategy validated (test restore)
  ☐ User accounts created for Phase 1 users
  ☐ Support team briefed and ready

D-1 (day before):
  ☐ Database backup taken
  ☐ All containers healthy
  ☐ Smoke test complete
  ☐ Communication sent to Phase 1 users

D-0 (go-live day):
  ☐ 08:00 — Final health check
  ☐ 09:00 — Phase 1 users briefed (training session)
  ☐ 09:30 — Go-Live authorized by management
  ☐ 10:00 — System opened for Phase 1 users
  ☐ Every 2 hours — Status check
  ☐ 17:00 — End-of-day review call

D+1:
  ☐ Review previous day's logs + errors
  ☐ Address any issues found
  ☐ Confirm Phase 1 user satisfaction
```

### Rollback Decision Criteria
```
IMMEDIATE ROLLBACK (no approval needed):
  - P1 incident not resolved within 1 hour
  - Data loss detected
  - Security breach detected
  - Database corruption detected

ROLLBACK AFTER CAB APPROVAL:
  - Error rate > 5% sustained for 30 minutes
  - More than 10 P2 incidents in first day
  - Business process blocked for > 2 hours

DO NOT ROLLBACK:
  - User adoption issues (training issue, not technical)
  - Minor UI complaints
  - Performance within SLA but "slower than expected"
```
