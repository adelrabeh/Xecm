# DARAH ECM — Enterprise Content Management System

> نظام إدارة المحتوى المؤسسي — دارة الملك عبدالعزيز  
> Version 1.0 (ECM Core) + Version 1.1 (xECM Extension)

---

## Overview

**DARAH ECM** is a mission-critical, enterprise-grade Enterprise Content Management platform designed for government institutions and large organizations. The system is fully bilingual (Arabic/English, RTL/LTR), built on ASP.NET Core (.NET 8), Microsoft SQL Server, and a React frontend — following Clean Architecture with a Modular Monolith approach.

The **xECM Extension (v1.1)** transforms it from a document-centric system into a **business-integrated content services platform**, where content is anchored to real business objects (Projects, Contracts, Cases, Customers) and metadata is synchronized from external systems (SAP, Salesforce, Oracle HR).

---

## Repository Structure

```
Xecm/
├── docs/
│   ├── DARAH_ECM_Architecture_v1.0.docx       # ECM Core architecture document
│   └── DARAH_ECM_xECM_Architecture_v1.1.docx  # xECM Extension architecture document
│
├── sql/
│   ├── DARAH_ECM_Schema.sql                    # Full SQL Server schema (v1.0) — run first
│   └── DARAH_ECM_xECM_Extension.sql            # xECM addendum schema (v1.1) — run after v1.0
│
├── src/
│   ├── Darah.ECM.Domain/
│   │   └── Entities/DomainEntities.cs          # BaseEntity, User, Document, WorkflowInstance, AuditLog
│   │
│   ├── Darah.ECM.Application/
│   │   └── ApplicationLayer.cs                 # CQRS Commands/Queries, DTOs, Validators, Interfaces
│   │
│   ├── Darah.ECM.Infrastructure/
│   │   └── InfrastructureLayer.cs              # EF Core DbContext, FileStorage, WorkflowEngine, Email, Jobs
│   │
│   ├── Darah.ECM.API/
│   │   ├── Controllers/ApiControllers.cs       # Auth, Documents, Workflow, Search, Admin, Audit, Reports
│   │   └── Middleware/ApiMiddlewareAndStartup.cs # JWT, Exception handling, Program.cs, CurrentUser, AuthService
│   │
│   └── Darah.ECM.xECM/                         # xECM Extension (v1.1)
│       ├── Domain/WorkspaceDomain.cs           # Workspace entity, Commands, DTOs
│       ├── Infrastructure/MetadataSyncEngine.cs # Sync engine, SAP connector, Salesforce connector, Hangfire job
│       └── API/WorkspaceControllers.cs         # WorkspacesController (22 endpoints), ExternalSystemsController
│
└── deploy/
    ├── docker-compose.yml                      # SQL Server + Redis + API + Frontend
    └── DEPLOYMENT_GUIDE.md                     # IIS, Secrets, CI/CD, Monitoring, DR, Security checklist
```

---

## Technology Stack

| Component | Technology |
|-----------|-----------|
| Backend | ASP.NET Core (.NET 8) |
| Database | Microsoft SQL Server 2022 |
| ORM | Entity Framework Core 8 |
| CQRS | MediatR 12 |
| Validation | FluentValidation 11 |
| Auth | JWT Bearer + HttpOnly Cookie (OAuth2/OIDC ready) |
| Background Jobs | Hangfire (SQL Server persistence) |
| Logging | Serilog → File + Seq/ELK |
| Caching | IMemoryCache + Redis |
| File Storage | IFileStorageService abstraction (Local → S3/Azure Blob) |
| API Docs | Swagger / OpenAPI 3.0 |
| Frontend | React 18 + Vite + TypeScript + Ant Design (RTL) |

---

## ECM Core Modules (v1.0)

| Module | Description |
|--------|-------------|
| Identity & Access (IAM) | Users, Roles, Permissions, JWT, SSO-ready, AD/LDAP-ready |
| Document Management | Upload, versioning, check-in/out, preview, libraries, folders |
| Records Management | Retention schedules, legal hold, lifecycle, disposal |
| Metadata Engine | Dynamic schema, 10+ field types, validation, templates |
| Workflow Engine | Multi-step approvals, SLA, escalation, delegation, inbox |
| Search & Retrieval | Full-text search, advanced filters, saved searches |
| Notifications | In-app + email (SMTP), event-driven, SLA alerts |
| Audit & Compliance | Immutable append-only log, all events, exportable |
| Reporting & Analytics | Executive + operational dashboards, KPIs, PDF/Excel export |
| Administration | All system configuration via admin APIs |

---

## xECM Extension (v1.1)

| Capability | Description |
|-----------|-------------|
| Business Workspace Layer | Project, Contract, Case, Customer, Employee, Department, General |
| External System Binding | SAP (OData), Salesforce (REST), Oracle HR, Generic REST |
| Metadata Sync Engine | Configurable field mappings, 4 conflict strategies, delta sync |
| Workspace-Driven Security | Policy inheritance to all documents, IsDeny support |
| Lifecycle Alignment | Archive/dispose workspace cascades to all documents |
| Sync Connectors | SAPConnector (OData v4), SalesforceConnector (REST v58) |
| Hangfire Jobs | Hourly bulk sync, delta sync, SLA check, retention check |

---

## Getting Started

### 1. Database Setup

```sql
-- 1. Run core schema (creates all tables, indexes, seed data)
sqlcmd -S . -d master -i sql/DARAH_ECM_Schema.sql

-- 2. Run xECM extension (adds Workspace tables)
sqlcmd -S . -d DARAH_ECM -i sql/DARAH_ECM_xECM_Extension.sql
```

### 2. Configuration

Copy `appsettings.json` template from `src/Darah.ECM.API/Middleware/ApiMiddlewareAndStartup.cs` (see comments at end of file) and set:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=DARAH_ECM;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "SecretKey": "YOUR_256BIT_SECRET_KEY",
    "Issuer": "darah.ecm.api",
    "Audience": "darah.ecm.client"
  },
  "Storage": {
    "LocalPath": "D:\\ECM_Storage"
  }
}
```

### 3. Docker (Development)

```bash
docker-compose -f deploy/docker-compose.yml up -d
```

### 4. API Access

- API: `http://localhost:8080`
- Swagger: `http://localhost:8080/swagger`
- Health: `http://localhost:8080/health`

---

## Security

- OWASP Top 10 mitigations applied throughout
- JWT (15 min) + HttpOnly refresh cookie (8 hr)
- BCrypt password hashing (cost factor 12)
- Append-only AuditLogs (no UPDATE/DELETE permissions for app user)
- GUID primary keys on Documents and Workspaces (prevents enumeration)
- Security headers: CSP, X-Frame-Options, HSTS, X-Content-Type-Options

---

## License

Internal / Confidential — دارة الملك عبدالعزيز  
© 2026 Digital Transformation Department
