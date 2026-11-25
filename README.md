# Vairas Document Management

School-owned document templates (DOCX) and generated, school-scoped training documents with private blob storage,
malware scanning, DOCX/PDF rendering, exact evidence snapshots from Learning Tracking and an auditable retention
trail (ASP.NET Core 10, PostgreSQL, EF Core/Npgsql, Azure Blob/Azurite, ClamAV, LibreOffice).

## Projects

| Project | Responsibility |
|---|---|
| `Vairas.DocumentManagement` | HTTP API: controller, `Configuration/Configure*`, security, owner clients, storage/scanner/renderer adapters, workers, operator commands (`Startup/`) |
| `Vairas.DocumentManagement.Business` | Document service and ports |
| `Vairas.DocumentManagement.Domain` | Document rules and entities |
| `Vairas.DocumentManagement.Persistance` | `DocumentDbContext`, migrations, store, delivery catalogue |
| `Vairas.DocumentManagement.Tests` | NUnit tests (PostgreSQL, Azurite and ClamAV via Testcontainers) |

## Dependencies

PostgreSQL 17, Authentication, User Management, School Management, Learning Tracking; Azure Blob Storage (Azurite
locally), ClamAV, a DOCX-to-PDF renderer (`soffice`) and optionally Service Bus. Local origin `https://localhost:7700`;
`compose.yaml` starts the local storage and scanner.

## Essential configuration

Options are bound and validated at startup (`Configuration/ConfigureOptions.cs`; checks are skipped only in the
Testing environment). Use `__` in environment variables.

| Section | Notes |
|---|---|
| `ConnectionStrings:DocumentManagement` | restricted runtime role |
| `ConnectionStrings:Delivery` | separate worker role; required when `Documents:WorkersEnabled` |
| `Authentication` | HTTPS `Authority`/`Issuer`, audiences, `WorkloadClients` allowlist |
| `Dependencies` | HTTPS origins including `LearningBaseUrl`; `ClientSecret` required |
| `Documents` | `PublicOrigin` (HTTPS), upload/render limits, `WorkersEnabled` |
| `Blob` | `ConnectionString` (Azurite) or `ServiceUri`; development storage is rejected outside Development |
| `Scanner`, `Renderer`, `ServiceBus` | ClamAV endpoint, renderer executable; a disabled broker needs no credentials |

## Run locally

```powershell
dotnet run --project Vairas.DocumentManagement --launch-profile https
dotnet run --project Vairas.DocumentManagement -- --provision-storage   # operator commands: --migrate, --grant-runtime, --seed-demo
```

The complete local stack is started from `../local/Start.ps1`. Database roles and grants: `database/`.

## Tests

```powershell
dotnet test Vairas.DocumentManagement.slnx       # Docker required
./scripts/verify.ps1 -SkipIntegration            # unit tests with the coverage gate
```

Contract notes: [CONTRACT-NOTES.md](CONTRACT-NOTES.md). API exports: `api/`. Extended notes, design sources,
validation reports and evidence: `../../Documentation/Backend/Vairas.DocumentManagement/`.
