param([switch]$SkipIntegration, [switch]$IncludeInfrastructure, [switch]$IncludeServices, [switch]$IncludeRender)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $root
try {
    $run = Join-Path $root ('evidence/run-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Force -Path $run | Out-Null
    dotnet restore Vairas.DocumentManagement.slnx
    if ($LASTEXITCODE -ne 0) { throw 'Restore or NuGet audit failed.' }
    dotnet build Vairas.DocumentManagement.slnx -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'EF tool restore failed.' }
    dotnet ef migrations has-pending-model-changes --project Vairas.DocumentManagement.Persistance --startup-project Vairas.DocumentManagement.Persistance --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'EF model differs from its snapshot.' }
    dotnet test Vairas.DocumentManagement.Tests -c Release --no-build --no-restore --filter 'Category!=Postgres&Category!=Infrastructure' --settings coverage.runsettings --collect:'XPlat Code Coverage' --logger 'trx;LogFileName=unit.trx' --results-directory (Join-Path $run 'unit')
    if ($LASTEXITCODE -ne 0) { throw 'Unit tests failed.' }
    $file = Get-ChildItem (Join-Path $run 'unit') -Filter coverage.cobertura.xml -Recurse | Select-Object -First 1
    if (-not $file) { throw 'No fresh unit coverage report.' }
    [xml]$report = Get-Content -LiteralPath $file.FullName
    $metrics = @{}
    foreach ($name in 'Vairas.DocumentManagement.Business','Vairas.DocumentManagement.Domain') {
        $package = $report.coverage.packages.package | Where-Object name -eq $name
        if (-not $package) { throw "Missing coverage assembly $name" }
        $percent = [Math]::Round(100*[double]$package.'line-rate',2); $metrics[$name] = $percent
        if ($percent -lt 70) { throw "Unit coverage below 70% for ${name}: $percent" }
    }
    $integration = 'NOT RUN'; $infrastructure = 'NOT RUN'; $services = 'NOT RUN'; $render = 'NOT RUN'
    if (-not $SkipIntegration) {
        dotnet test Vairas.DocumentManagement.Tests -c Release --no-build --no-restore --filter Category=Postgres --logger 'trx;LogFileName=postgres.trx' --results-directory (Join-Path $run 'postgres')
        if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL integration tests failed.' }; $integration = 'PASSED'
    }
    if ($IncludeInfrastructure) {
        dotnet test Vairas.DocumentManagement.Tests -c Release --no-build --no-restore --filter Category=Infrastructure --logger 'trx;LogFileName=infrastructure.trx' --results-directory (Join-Path $run 'infrastructure')
        if ($LASTEXITCODE -ne 0) { throw 'Real Azurite/ClamAV tests failed.' }; $infrastructure = 'PASSED'
    }
    if ($IncludeServices) {
        $learning = Join-Path $root '../Vairas.LearningTracking'
        if (-not (Test-Path (Join-Path $learning 'Vairas.LearningTracking.slnx'))) { throw 'Sibling service sources required for the six-service suite.' }
        Push-Location $learning
        try {
            dotnet restore Vairas.LearningTracking.slnx
            if ($LASTEXITCODE -ne 0) { throw 'Sibling restore failed.' }
            dotnet test Vairas.LearningTracking.slnx -c Release --no-restore --filter FullyQualifiedName~FiveServiceTests --logger 'trx;LogFileName=six-services.trx' --results-directory (Join-Path $run 'services')
            if ($LASTEXITCODE -ne 0) { throw 'Six-service HTTP suite failed.' }; $services = 'PASSED'
        } finally { Pop-Location }
    }
    if ($IncludeRender) { & (Join-Path $PSScriptRoot 'verify-render.ps1'); $render = 'PASSED' }
    Push-Location scripts/contract-tools
    try { $npmCommand = if ($env:OS -eq 'Windows_NT') { 'npm.cmd' } else { 'npm' }; & $npmCommand ci --ignore-scripts --no-fund; if ($LASTEXITCODE -ne 0) { throw 'Contract tooling restore failed.' } } finally { Pop-Location }
    Push-Location scripts/contract-tools
    try { & $npmCommand audit --audit-level=low; if ($LASTEXITCODE -ne 0) { throw 'Contract tooling vulnerability audit failed.' } } finally { Pop-Location }
    node scripts/validate-contracts.cjs
    if ($LASTEXITCODE -ne 0) { throw 'Contract/source validation failed.' }
    node scripts/check-links.cjs
    if ($LASTEXITCODE -ne 0) { throw 'Local documentation link validation failed.' }
    $audit = dotnet list Vairas.DocumentManagement.slnx package --vulnerable --include-transitive --format json
    if ($LASTEXITCODE -ne 0) { throw 'NuGet vulnerability query failed.' }
    $audit | Set-Content -Encoding UTF8 (Join-Path $run 'dependency-audit.json')
    $data = ($audit -join "`n") | ConvertFrom-Json
    foreach ($project in $data.projects) { foreach ($framework in $project.frameworks) { foreach ($package in @($framework.topLevelPackages)+@($framework.transitivePackages)) { if ($package.vulnerabilities) { throw 'Vulnerable package reported.' } } } }
    @{verifiedAt=(Get-Date).ToUniversalTime().ToString('o');unitCoverage=$metrics;postgres=$integration;infrastructure=$infrastructure;services=$services;render=$render;brokerTransport='NOT RUN'} | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 (Join-Path $run 'verification-summary.json')
    $rows = ($metrics.GetEnumerator() | ForEach-Object { "<tr><td>$($_.Key)</td><td>$($_.Value)%</td></tr>" }) -join ''
    "<!doctype html><html lang='en'><meta charset='utf-8'><title>Document Management verification</title><h1>Unit coverage</h1><table>$rows</table><p>PostgreSQL: $integration. Infrastructure: $infrastructure. Services: $services. Renderer: $render.</p>" | Set-Content -Encoding UTF8 (Join-Path $run 'coverage.html')
    Write-Output "Verification passed: $run"
} finally { Pop-Location }
