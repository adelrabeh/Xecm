# ============================================================
# config/environments/appsettings.Development.json
# ============================================================
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=DARAH_ECM_DEV;Trusted_Connection=True;TrustServerCertificate=True;",
    "Redis": ""
  },
  "Jwt": {
    "SecretKey": "dev-only-key-minimum-32-chars-NOT-for-prod",
    "Issuer": "darah.ecm.api.dev",
    "Audience": "darah.ecm.client.dev",
    "ExpiryMinutes": 60
  },
  "Auth": {
    "MaxFailedAttempts": 10,
    "LockoutDurationMinutes": 5
  },
  "Storage": {
    "Provider": "LocalFileSystem",
    "LocalPath": "C:\\ECM_Storage_Dev"
  },
  "Email": {
    "SmtpHost": "localhost",
    "SmtpPort": "1025",
    "SmtpUsername": "",
    "SmtpPassword": "",
    "FromAddress": "dev@ecm.local",
    "FromName": "ECM Dev"
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:5173", "http://localhost:3000"]
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": { "Microsoft": "Information", "Hangfire": "Information" }
    }
  },
  "Swagger": { "Enabled": true },
  "Features": {
    "MaintenanceMode": false,
    "DebugEndpoints": true,
    "MockExternalSystems": true
  }
}

---
# ============================================================
# config/environments/appsettings.Test.json
# ============================================================
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=sqltest;Database=DARAH_ECM_TEST;User Id=ecm_app;Password=#{TEST_DB_PASSWORD}#;TrustServerCertificate=True;",
    "Redis": "redis-test:6379"
  },
  "Jwt": {
    "SecretKey": "#{TEST_JWT_SECRET}#",
    "Issuer": "darah.ecm.api.test",
    "Audience": "darah.ecm.client.test",
    "ExpiryMinutes": 30
  },
  "Auth": {
    "MaxFailedAttempts": 5,
    "LockoutDurationMinutes": 15
  },
  "Storage": {
    "Provider": "LocalFileSystem",
    "LocalPath": "D:\\ECM_Storage_Test"
  },
  "Email": {
    "SmtpHost": "#{TEST_SMTP_HOST}#",
    "SmtpPort": "587",
    "SmtpUsername": "#{TEST_SMTP_USER}#",
    "SmtpPassword": "#{TEST_SMTP_PASS}#",
    "FromAddress": "ecm-test@darah.gov.sa",
    "FromName": "DARAH ECM Test"
  },
  "Cors": {
    "AllowedOrigins": ["https://ecm-test.darah.gov.sa"]
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": { "Microsoft": "Warning", "Hangfire": "Warning" }
    }
  },
  "Swagger": { "Enabled": true },
  "Features": {
    "MaintenanceMode": false,
    "DebugEndpoints": false,
    "MockExternalSystems": false
  }
}

---
# ============================================================
# config/environments/appsettings.Production.json
# ALL SECRETS MUST BE SET VIA ENVIRONMENT VARIABLES or secrets manager.
# This file contains NON-SECRET settings only.
# ============================================================
{
  "Jwt": {
    "Issuer": "darah.ecm.api",
    "Audience": "darah.ecm.client",
    "ExpiryMinutes": 15
  },
  "Auth": {
    "MaxFailedAttempts": 5,
    "LockoutDurationMinutes": 30
  },
  "Storage": {
    "Provider": "LocalFileSystem",
    "LocalPath": "D:\\ECM_Storage"
  },
  "Email": {
    "FromAddress": "ecm-noreply@darah.gov.sa",
    "FromName": "نظام ECM - دارة الملك عبدالعزيز"
  },
  "Cors": {
    "AllowedOrigins": ["https://ecm.darah.gov.sa"]
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Warning",
      "Override": { "Microsoft": "Error", "Hangfire": "Warning" }
    }
  },
  "Swagger": { "Enabled": false },
  "Features": {
    "MaintenanceMode": false,
    "DebugEndpoints": false,
    "MockExternalSystems": false
  }
}

---
# ============================================================
# SECRETS MANAGEMENT GUIDE
# NEVER commit actual secrets. Use one of these approaches:
# ============================================================

# Approach 1: Windows Environment Variables (IIS)
# Set via PowerShell as Machine-level env vars:
#
# [Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection",
#     "Server=sqlprod;...", [EnvironmentVariableTarget]::Machine)
# [Environment]::SetEnvironmentVariable("Jwt__SecretKey",
#     "STRONG_256BIT_KEY", [EnvironmentVariableTarget]::Machine)
# [Environment]::SetEnvironmentVariable("Email__SmtpPassword",
#     "SMTP_PASS", [EnvironmentVariableTarget]::Machine)

# Approach 2: GitHub Actions Secrets (CI/CD)
# Store in GitHub repo → Settings → Secrets → Actions
# Reference as ${{ secrets.PROD_DB_PASSWORD }}

# Approach 3: Azure Key Vault (Enterprise)
# Install Microsoft.Extensions.Configuration.AzureKeyVault
# builder.Configuration.AddAzureKeyVault(
#     new Uri($"https://{keyVaultName}.vault.azure.net/"),
#     new DefaultAzureCredential());

# ============================================================
# REQUIRED SECRETS (set externally, never in files):
# ============================================================
# ConnectionStrings__DefaultConnection   → Full SQL Server connection string
# Jwt__SecretKey                         → 256-bit random key (openssl rand -base64 32)
# Email__SmtpPassword                    → SMTP server password
# ExternalSystems__SAP__Password         → SAP OData credentials
# ExternalSystems__SF__ClientSecret      → Salesforce Connected App secret
