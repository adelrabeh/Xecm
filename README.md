# DARAH ECM — Enterprise Content Management Platform

> نظام إدارة المحتوى المؤسسي — دارة الملك عبدالعزيز  
> **v1.0** ECM Core + **v1.1** xECM Extension  
> Architecture: Clean Architecture + Modular Monolith → Microservices-ready

---

## Repository Structure

```
Xecm/
├── .github/workflows/ci-cd.yml              # Build→Test→Security Scan→Deploy (TEST+PROD)
├── config/environments/                     # Dev / Test / Production configs + secrets guide
├── docs/                                    # Architecture documents (Word)
├── sql/                                     # SQL Server schemas
├── src/
│   ├── Darah.ECM.Domain/                    # Domain Layer (no dependencies)
│   │   ├── Common/BaseEntity.cs             # BaseEntity, IAggregateRoot, IDomainEvent
│   │   ├── ValueObjects/ValueObjects.cs     # DocumentStatus (with transitions), ClassificationLevel, RetentionPeriod, FileMetadata, Money
│   │   ├── Entities/Entities.cs             # User, Document, DocumentVersion, WorkflowInstance, WorkflowTask, AuditLog
│   │   ├── Aggregates/Aggregates.cs         # DocumentAggregate, WorkspaceAggregate
│   │   ├── Events/DomainEvents.cs           # 13 domain events across Document, Workflow, Records, Workspace domains
│   │   └── Interfaces/Interfaces.cs         # IRepository, IUnitOfWork, IFileStorageService, IEmailService, IAuditService, IEventBus, ICurrentUser, IExternalSystemConnector
│   ├── Darah.ECM.Application/               # Application Layer (CQRS)
│   │   ├── Common/CommonModels.cs           # ApiResponse<T>, PagedResult<T>, ValidationBehavior, LoggingBehavior
│   │   ├── Documents/DocumentsModule.cs     # Commands, Queries, Handlers, DTOs
│   │   └── Workflow/WorkflowModule.cs       # Workflow Commands/Queries + 5 Event Handlers
│   ├── Darah.ECM.Infrastructure/            # Infrastructure Layer
│   │   └── Infrastructure.cs               # EcmDbContext, UnitOfWork, Repositories, LocalFileStorageService, S3FileStorageService, InProcessEventBus, CurrentUserService, JwtTokenService, AuditService
│   ├── Darah.ECM.API/                       # Presentation Layer
│   │   └── API.cs                          # GlobalExceptionFilter, RequirePermissionAttribute, AuthController, DocumentsController, WorkflowController, SearchController, DI Extensions, ProgramStartup
│   ├── Darah.ECM.xECM/                      # xECM Extension Module
│   │   └── xECM.cs                         # Workspace Entity, Commands, Queries, DTOs, MetadataSyncEngine, SAPConnector, SalesforceConnector, WorkspacesController (15 endpoints), ExternalSystemsController
│   └── Darah.ECM.Shared/                    # Shared Kernel
│       └── Shared.cs                       # Permissions, SystemRoles, AuditEventTypes, Extensions, FileHelper, SecurityHelper
├── tests/
│   └── Darah.ECM.UnitTests/UnitTests.cs    # 29 unit tests across Domain + Application layers
└── deploy/                                  # Docker Compose + Deployment Guide
```

---

## Architecture

### Clean Architecture Layers
```
┌──────────────────────────────────────────┐
│  Presentation (API)                       │  Controllers, Filters, Models
├──────────────────────────────────────────┤
│  Application (Use Cases)                  │  Commands, Queries, Handlers, DTOs
├──────────────────────────────────────────┤
│  Domain (Business Logic)                  │  Entities, ValueObjects, Aggregates, Events
├──────────────────────────────────────────┤
│  Infrastructure (I/O)                     │  EF Core, FileStorage, Email, EventBus
└──────────────────────────────────────────┘
         ↑  Dependencies flow inward only  ↑
```

### Key Patterns

| Pattern | Location |
|---------|---------|
| CQRS + MediatR | Application/Documents, Application/Workflow |
| Aggregate Root | Domain/Aggregates |
| Domain Events | Domain/Events → Infrastructure/Messaging (InProcessEventBus) |
| Value Objects | Domain/ValueObjects (immutable, self-validating) |
| Unit of Work | Infrastructure/Persistence |
| Repository | Domain/Interfaces + Infrastructure/Persistence |
| Pipeline Behaviors | Application/Common (Validation, Logging) |
| Provider Abstraction | IFileStorageService (Local → S3 / Azure Blob) |
| Event Bus | IEventBus (In-process → MassTransit for microservices) |

### DocumentStatus Transition Matrix
```
Draft      → Active | Pending | Archived
Active     → Pending | Archived | Superseded
Pending    → Approved | Rejected | Active
Approved   → Active | Archived | Superseded
Rejected   → Draft | Active
Archived   → Disposed
Disposed   → (terminal)
```

---

## Domain Events

| Event | Trigger | Handler |
|-------|---------|---------|
| `DocumentCreatedEvent` | `Document.Create()` | Audit |
| `DocumentApprovedEvent` | Status → Approved | Email creator, update workspace |
| `DocumentArchivedEvent` | Status → Archived | Audit |
| `WorkflowCompletedEvent` | Final step approved | Audit |
| `SLABreachedEvent` | Hangfire job | Email assignee + supervisor |
| `WorkspaceCreatedEvent` | `Workspace.Create()` | Audit |
| `WorkspaceLinkedToExternalEvent` | `Workspace.BindToExternal()` | Trigger initial sync |
| `WorkspaceArchivedEvent` | `Workspace.Archive()` | Cascade archive to documents |
| `WorkspaceLegalHoldAppliedEvent` | `Workspace.ApplyLegalHold()` | Cascade hold to documents |
| `MetadataSyncCompletedEvent` | MetadataSyncEngine | Audit |
| `MetadataSyncFailedEvent` | MetadataSyncEngine | Alert admin |
| `RetentionExpiredEvent` | Hangfire job | Flag for disposal, audit |
| `RecordDeclaredEvent` | Records module | Records registry |

---

## xECM Extension (v1.1)

```
ECM v1.0:  Document → Library → Folder
xECM v1.1: Document → Workspace → Business Entity (SAP / CRM / HR)
```

| Capability | Detail |
|-----------|--------|
| Workspace Types | Project, Contract, Case, Customer, Employee, Department, General |
| External Binding | One workspace ↔ one external object (DB UNIQUE enforced) |
| SAP Connector | OData v4: WBSElement, PurchaseOrder, Contract |
| Salesforce Connector | REST v58: Account, Opportunity, Case, Contact |
| Sync Engine | Configurable field mappings, 4 conflict strategies |
| Conflict Strategies | ExternalWins \| InternalWins \| Newer \| Manual |
| Security | Workspace policies cascade to all bound documents |
| Lifecycle | Archive / LegalHold / Dispose cascades to documents |

---

## Technology Stack

| Component | Technology |
|-----------|-----------|
| Backend | ASP.NET Core .NET 8 LTS |
| Database | SQL Server 2022 + Full-Text Search |
| ORM | Entity Framework Core 8 (Code-First, value converters for ValueObjects) |
| CQRS | MediatR 12 |
| Validation | FluentValidation 11 |
| Authentication | JWT (15min) + HttpOnly refresh cookie (8hr) |
| Background Jobs | Hangfire (SQL Server persistence) |
| Logging | Serilog → File + Seq/ELK |
| Caching | IMemoryCache → Redis |
| File Storage | IFileStorageService (Local → S3/Azure Blob, swap without code changes) |
| Event Bus | InProcessEventBus → MassTransit/RabbitMQ (interface-compatible) |
| API Docs | Swagger/OpenAPI 3.0, versioned (/api/v1/) |
| Tests | xUnit + Moq (29 unit tests) |
| CI/CD | GitHub Actions (build → test → scan → deploy) |

---

## API Standards

```
Base URL:   /api/v1/[controller]
Auth:       Bearer {jwt} in Authorization header
Response:   ApiResponse<T> on all endpoints
Pagination: PagedResult<T>
Errors:     GlobalExceptionFilter → standard ProblemDetails shape
```

Response envelope:
```json
{
  "success": true,
  "message": "تم رفع الوثيقة بنجاح",
  "data": { ... },
  "errors": [],
  "timestamp": "2026-04-11T10:00:00Z",
  "traceId": "abc123"
}
```

---

## Getting Started

```bash
# 1. Database
sqlcmd -S . -i sql/DARAH_ECM_Schema.sql
sqlcmd -S . -d DARAH_ECM -i sql/DARAH_ECM_xECM_Extension.sql

# 2. Set secrets (never in appsettings.json)
export ConnectionStrings__DefaultConnection="Server=.;Database=DARAH_ECM;..."
export Jwt__SecretKey="your-256-bit-key"

# 3. Docker (Dev)
docker-compose -f deploy/docker-compose.yml up -d

# 4. API
# Swagger:  http://localhost:8080/swagger
# Health:   http://localhost:8080/health

# 5. Tests
dotnet test tests/Darah.ECM.UnitTests/
```

---

## CI/CD

```
Push → develop:  Build → Unit Tests → Security Scan → Deploy TEST → Health Check
Push → main:     Build → Unit Tests → Security Scan → Manual Approval → Deploy PROD → Health Check → Auto-rollback on failure
```

---

## Microservices Roadmap

Each module can be extracted without rewriting business logic:

| Module | Future Service | Key Change |
|--------|---------------|-----------|
| Documents | `ecm-documents-svc` | Extract Application + Infrastructure |
| Workflow | `ecm-workflow-svc` | Replace InProcessEventBus → MassTransit |
| xECM | `ecm-workspace-svc` | Extract xECM module |
| Identity | `ecm-auth-svc` | Add Keycloak / IdentityServer |
| Metadata Sync | `ecm-sync-svc` | Extract sync engine + connectors |

---

## Security

- OWASP Top 10 mitigations throughout
- BCrypt password hashing (work factor 12)
- AuditLogs: INSERT-only DB permission for app user (tamper-proof)
- GUID PKs on Documents and Workspaces (prevents enumeration)
- Explicit Deny overrides any Allow at same or lower permission level
- Security headers: CSP, X-Frame-Options, HSTS, Referrer-Policy
- File upload: extension allowlist + SHA-256 integrity verification

---

**Classification:** Internal / Confidential  
**Owner:** Executive Department of Digital Transformation — دارة الملك عبدالعزيز  
© 2026
