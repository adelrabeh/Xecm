# DARAH ECM — Deployment & Operations Guide
# دليل النشر والتشغيل
# ============================================================

## 1. PREREQUISITES / المتطلبات الأساسية

### Production Server
- OS: Windows Server 2022 Standard / Datacenter (x64)
- CPU: Minimum 8 vCPU (16 recommended)
- RAM: Minimum 16 GB (32 GB recommended)
- Disk (OS): 100 GB SSD
- Disk (Storage): 2 TB+ (ECM file storage; expandable NAS)
- .NET 8 Runtime (ASP.NET Core)
- IIS 10 + URL Rewrite Module + ARR
- SQL Server 2022 Standard or Enterprise

### CI/CD Prerequisites
- Git repository (GitHub / Azure DevOps)
- Build agent with .NET 8 SDK
- Access to production server for deployment

---

## 2. SQL SERVER SETUP

```sql
-- Run as sysadmin
CREATE DATABASE DARAH_ECM COLLATE Arabic_CI_AI;

-- Create dedicated application user (NOT sa)
CREATE LOGIN ecm_app WITH PASSWORD = 'STRONG_PASSWORD_HERE';
USE DARAH_ECM;
CREATE USER ecm_app FOR LOGIN ecm_app;
ALTER ROLE db_datareader ADD MEMBER ecm_app;
ALTER ROLE db_datawriter ADD MEMBER ecm_app;
GRANT EXECUTE TO ecm_app;

-- Enable Full-Text Search (requires FTS feature)
EXEC sp_fulltext_database 'enable';

-- Run schema script
-- DARAH_ECM_Schema.sql
```

### Backup Strategy (3-2-1-1-0)
```sql
-- Daily full backup at 01:00
BACKUP DATABASE DARAH_ECM
TO DISK = 'D:\Backups\DARAH_ECM_Full_' + FORMAT(GETDATE(), 'yyyyMMdd') + '.bak'
WITH COMPRESSION, CHECKSUM, STATS = 5;

-- Hourly differential
BACKUP DATABASE DARAH_ECM
TO DISK = 'D:\Backups\DARAH_ECM_Diff_' + FORMAT(GETDATE(), 'yyyyMMdd_HH') + '.bak'
WITH DIFFERENTIAL, COMPRESSION;

-- Continuous transaction log (every 15 minutes)
BACKUP LOG DARAH_ECM
TO DISK = 'D:\Backups\Logs\DARAH_ECM_Log_' + FORMAT(GETDATE(), 'yyyyMMdd_HHmm') + '.trn';
```

---

## 3. IIS DEPLOYMENT

### Application Pool
```powershell
# Create Application Pool
New-WebAppPool -Name "ECM_API_Pool"
Set-ItemProperty IIS:\AppPools\ECM_API_Pool -Name processModel.identityType -Value "ApplicationPoolIdentity"
Set-ItemProperty IIS:\AppPools\ECM_API_Pool -Name managedRuntimeVersion -Value ""
Set-ItemProperty IIS:\AppPools\ECM_API_Pool -Name startMode -Value "AlwaysRunning"
Set-ItemProperty IIS:\AppPools\ECM_API_Pool -Name recycling.periodicRestart.time -Value "00:00:00"

# Create Website
New-Website -Name "ECM_API" -PhysicalPath "D:\ECM\api" -ApplicationPool "ECM_API_Pool" -Port 8080
New-Website -Name "ECM_Frontend" -PhysicalPath "D:\ECM\web" -ApplicationPool "ECM_API_Pool" -Port 443
```

### web.config (API)
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
    </handlers>
    <aspNetCore processPath="dotnet"
                arguments=".\Darah.ECM.API.dll"
                stdoutLogEnabled="true"
                stdoutLogFile=".\logs\stdout"
                hostingModel="inprocess">
      <environmentVariables>
        <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
      </environmentVariables>
    </aspNetCore>
    <security>
      <requestFiltering>
        <requestLimits maxAllowedContentLength="536870912" /><!-- 512 MB -->
      </requestFiltering>
    </security>
  </system.webServer>
</configuration>
```

---

## 4. SECRETS MANAGEMENT

**NEVER store secrets in appsettings.json in production.**

### Windows Environment Variables (Recommended for IIS)
```powershell
# Set environment variables at machine level for IIS
[System.Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection",
    "Server=sqlprod;Database=DARAH_ECM;User Id=ecm_app;Password=STRONG_PASS;TrustServerCertificate=True;",
    [System.EnvironmentVariableTarget]::Machine)

[System.Environment]::SetEnvironmentVariable("Jwt__SecretKey",
    "GENERATE_A_STRONG_256BIT_KEY_HERE",
    [System.EnvironmentVariableTarget]::Machine)

[System.Environment]::SetEnvironmentVariable("Email__SmtpPassword",
    "SMTP_PASSWORD_HERE",
    [System.EnvironmentVariableTarget]::Machine)
```

### Production appsettings.Production.json (non-secrets only)
```json
{
  "Jwt": {
    "Issuer": "darah.ecm.api",
    "Audience": "darah.ecm.client",
    "ExpiryMinutes": 15
  },
  "Storage": {
    "LocalPath": "D:\\ECM_Storage"
  },
  "Serilog": {
    "MinimumLevel": { "Default": "Warning" }
  }
}
```

---

## 5. CI/CD PIPELINE (GitHub Actions / Azure DevOps)

```yaml
# .github/workflows/deploy-prod.yml
name: Deploy to Production

on:
  push:
    branches: [main]

jobs:
  build-and-deploy:
    runs-on: windows-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore src/Darah.ECM.sln

      - name: Build
        run: dotnet build src/Darah.ECM.sln --configuration Release --no-restore

      - name: Test
        run: dotnet test src/tests/Darah.ECM.UnitTests --configuration Release --no-build --verbosity normal

      - name: Publish API
        run: dotnet publish src/Darah.ECM.API/Darah.ECM.API.csproj -c Release -o ./publish/api

      - name: Deploy to IIS
        uses: SamKirkland/FTP-Deploy-Action@v4.3.4
        with:
          server: ${{ secrets.PROD_SERVER }}
          username: ${{ secrets.PROD_FTP_USER }}
          password: ${{ secrets.PROD_FTP_PASS }}
          local-dir: ./publish/api/
          server-dir: /ECM/api/
```

---

## 6. MONITORING & ALERTS

### Serilog → Seq Setup
```powershell
# Install Seq (logging server)
winget install Datalust.Seq
# Access at http://localhost:5341

# Configure alert: Error count > 10/min → email admin
```

### Windows Performance Counters to Monitor
| Counter | Warning | Critical |
|---------|---------|---------|
| CPU Usage | > 70% | > 90% |
| Available RAM | < 4 GB | < 2 GB |
| Disk Free (Storage) | < 100 GB | < 20 GB |
| SQL: Active Connections | > 150 | > 200 |
| API Response Time (avg) | > 1000ms | > 3000ms |

### Health Check Endpoints
```
GET /health              → Full health status (DB, disk, Hangfire)
GET /health/live         → Liveness probe (is process alive)
GET /health/ready        → Readiness probe (can serve traffic)
```

---

## 7. INITIAL SYSTEM SETUP CHECKLIST

After deployment, complete in order:

```
[ ] 1. Run SQL Schema: DARAH_ECM_Schema.sql
[ ] 2. Create admin user via SQL:
        INSERT INTO Users (Username, Email, PasswordHash, FullNameAr, IsActive)
        VALUES ('admin', 'admin@darah.gov.sa', '<BCRYPT_HASH>', 'مدير النظام', 1)
[ ] 3. Assign SystemAdmin role to admin user
[ ] 4. Verify /health returns Healthy
[ ] 5. Verify /swagger accessible (development only)
[ ] 6. Test login via POST /api/v1/auth/login
[ ] 7. Configure SMTP settings via Admin > System Settings
[ ] 8. Create initial DocumentLibraries
[ ] 9. Create initial DocumentTypes and MetadataFields
[ ] 10. Create departments and users
[ ] 11. Configure Workflow definitions
[ ] 12. Verify Hangfire dashboard at /admin/jobs
[ ] 13. Verify SLA check job is scheduled
[ ] 14. Set up backup jobs (SQL + file storage)
[ ] 15. Configure monitoring alerts
[ ] 16. Conduct security scan (OWASP ZAP or Burp Suite)
[ ] 17. Performance test (k6 or JMeter)
[ ] 18. Train key users (Admin, Document Managers)
[ ] 19. Go-live sign-off
```

---

## 8. DISASTER RECOVERY PROCEDURE

### RTO Target: 4 hours | RPO Target: 1 hour

```
1. Assess failure scope (server / DB / storage / network)
2. Notify IT management and stakeholders
3. Restore latest SQL backup to DR server
4. Mount latest file storage backup
5. Update DNS / Load Balancer to point to DR server
6. Verify /health endpoint on DR server
7. Notify users of service restoration
8. Document incident in change log
9. Post-incident review within 48 hours
```

---

## 9. SECURITY HARDENING CHECKLIST

```
[ ] TLS 1.2+ only (disable TLS 1.0/1.1)
[ ] Valid SSL certificate installed (not self-signed)
[ ] HSTS enabled in production
[ ] Security headers verified (Mozilla Observatory score A or higher)
[ ] SQL Server: disable SA account, enable auditing
[ ] Storage folder: no IIS direct access (serve via API only)
[ ] Firewall: block all except 443 (HTTPS), 8080 (internal), 1433 (internal)
[ ] Enable Windows Defender / AV on server
[ ] Penetration test completed before go-live
[ ] API rate limiting enabled
[ ] File upload: extension allowlist enforced
[ ] Swagger UI disabled in Production
[ ] Admin dashboard (Hangfire) protected by SystemAdmin role
```
