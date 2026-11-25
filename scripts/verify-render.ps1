param([string]$Image = 'vairas-documents-render-qa:local', [switch]$NoCache)
$ErrorActionPreference = 'Stop'
function Invoke-Docker([string[]]$DockerArguments) {
    $previous = $ErrorActionPreference
    try { $ErrorActionPreference = 'Continue'; & docker @DockerArguments 2>&1 | ForEach-Object { Write-Output "$_" }; $dockerExit = $LASTEXITCODE }
    finally { $ErrorActionPreference = $previous }
    if ($dockerExit -ne 0) { throw "Docker failed with exit code $dockerExit." }
}
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$output = Join-Path $root '.evidence/render'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$buildArguments = @('build','--target','qa','-t',$Image)
if ($NoCache) { $buildArguments += '--no-cache' }
$buildArguments += $root
Invoke-Docker $buildArguments
Invoke-Docker @('run','--rm','--network','none','-e','VAIRAS_ARTIFACT_DIRECTORY=/artifacts','--mount',"type=bind,source=$output,target=/artifacts",$Image,'--render-fixtures')
foreach ($kind in 'TrainingContract','PracticalJournal','CompletionReport') {
    Invoke-Docker @('run','--rm','--network','none','--entrypoint','pdftoppm','--mount',"type=bind,source=$output,target=/artifacts",$Image,'-scale-to','1400','-png',"/artifacts/$kind.pdf","/artifacts/$kind")
}
Write-Output "Rendered DOCX/PDF and page PNGs: $output"
