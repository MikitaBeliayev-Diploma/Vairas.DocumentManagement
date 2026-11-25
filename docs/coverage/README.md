# Vairas.DocumentManagement — code coverage

**Line coverage 82.86 %** (1252/1511 executable lines), branch coverage 65.99 % — threshold 70 % **met**. Generated 2026-11-25.

| Assembly | Line % | Lines | Branch % |
|---|---:|---:|---:|
| Vairas.DocumentManagement | 69.08 | 525/760 | 52.6 |
| Vairas.DocumentManagement.Business | 97.97 | 626/639 | 74.7 |
| Vairas.DocumentManagement.Domain | 100.0 | 15/15 | 93.48 |
| Vairas.DocumentManagement.Persistance | 88.66 | 86/97 | 89.19 |

## How it was measured

- Tool: coverlet (`XPlat Code Coverage`) driven by `Backend/local/coverage.py`; settings `Vairas.DocumentManagement/coverage.runsettings` (migrations and test assemblies excluded).
- Test projects: `Vairas.DocumentManagement.Tests`; line hits of all test projects of the service are merged (maximum per line), so shared code is never counted twice.
- Filter: `TestCategory!=Broker`. Broker tests need the Service Bus emulator and are executed (and passed) separately by `local/test_all.py`.
- The numbers are measured line coverage, not a statement about functional completeness; acceptance status is tracked in `Documentation/Final-Verification/AC-Traceability-Matrix.xlsx`.

## Reproduce

```powershell
cd Backend
py -3 local/coverage.py --services Vairas.DocumentManagement --filter "TestCategory!=Broker" --threshold 70
```

## Files

`coverage-summary.json` (machine-readable, per file) and `index.html` (per-file table, lowest coverage first) are in this folder.

## Least covered files

| File | Line % | Lines |
|---|---:|---:|
| `Startup/DatabaseCommands.cs` | 0.0 | 0/7 |
| `Startup/DemoFiles.cs` | 0.0 | 0/18 |
| `Startup/DemoSeed.cs` | 0.0 | 0/16 |
| `Integrations/DependencyCorrelationHandler.cs` | 0.0 | 0/7 |
| `Integrations/DependencyTransport.cs` | 0.0 | 0/21 |
| `Integrations/DocumentOwners.cs` | 0.0 | 0/28 |
| `Integrations/WorkloadTokens.cs` | 0.0 | 0/24 |
| `Vairas.DocumentManagement.Persistance/DocumentDesignFactory.cs` | 0.0 | 0/2 |
