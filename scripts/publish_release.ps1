param(
    [string]$ProjectPath = ".\GuaranteeManager.csproj",
    [string]$SolutionPath = ".\my_work.sln",
    [string]$Configuration = "Release",
    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-ProjectProperty {
    param(
        [xml]$ProjectXml,
        [string]$Name
    )

    foreach ($propertyGroup in $ProjectXml.Project.PropertyGroup) {
        $value = $propertyGroup.$Name
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return [string]$value
        }
    }

    return $null
}

function Assert-PathWithin {
    param(
        [string]$BasePath,
        [string]$CandidatePath
    )

    $baseFullPath = [System.IO.Path]::GetFullPath($BasePath)
    $candidateFullPath = [System.IO.Path]::GetFullPath($CandidatePath)

    if (-not $candidateFullPath.StartsWith($baseFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate outside releases root. Candidate: $candidateFullPath"
    }
}

$resolvedProjectPath = Resolve-Path -LiteralPath $ProjectPath
$resolvedSolutionPath = Resolve-Path -LiteralPath $SolutionPath
$repoRoot = Split-Path -Parent $resolvedProjectPath.Path
[xml]$projectXml = Get-Content -LiteralPath $resolvedProjectPath.Path

$projectName = [System.IO.Path]::GetFileNameWithoutExtension($resolvedProjectPath.Path)
$version = Get-ProjectProperty -ProjectXml $projectXml -Name "Version"
$runtimeIdentifier = Get-ProjectProperty -ProjectXml $projectXml -Name "RuntimeIdentifier"

if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Project version was not found in $ProjectPath."
}

if ([string]::IsNullOrWhiteSpace($runtimeIdentifier)) {
    throw "Project runtime identifier was not found in $ProjectPath."
}

$versionTag = if ($version.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) { $version } else { "v$version" }
$releasesRoot = Join-Path $repoRoot "releases"
$publishDirectory = Join-Path $releasesRoot $versionTag
$zipPath = Join-Path $releasesRoot ("{0}_{1}_{2}.zip" -f $projectName, $versionTag, $runtimeIdentifier)
$releaseNotesPath = Join-Path $repoRoot ("Doc\releases\README_{0}.md" -f $versionTag)

Assert-PathWithin -BasePath $releasesRoot -CandidatePath $publishDirectory
Assert-PathWithin -BasePath $releasesRoot -CandidatePath $zipPath

Write-Host "==> Building $projectName $versionTag ($runtimeIdentifier)"
dotnet build $resolvedSolutionPath.Path -c $Configuration

if (-not $SkipTests) {
    Write-Host "==> Running tests"
    dotnet test $resolvedSolutionPath.Path -c $Configuration --no-build
}

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Force -Path $publishDirectory | Out-Null

Write-Host "==> Publishing to $publishDirectory"
dotnet publish $resolvedProjectPath.Path `
    -c $Configuration `
    -r $runtimeIdentifier `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishReadyToRun=false `
    -o $publishDirectory

if (Test-Path -LiteralPath $releaseNotesPath) {
    Copy-Item -LiteralPath $releaseNotesPath -Destination (Join-Path $publishDirectory "README.md") -Force
}

Write-Host "==> Creating archive $zipPath"
Push-Location $releasesRoot
try {
    Compress-Archive -Path $versionTag -DestinationPath $zipPath -CompressionLevel Optimal
}
finally {
    Pop-Location
}

$result = [PSCustomObject]@{
    Project = $projectName
    Version = $versionTag
    RuntimeIdentifier = $runtimeIdentifier
    PublishDirectory = $publishDirectory
    Archive = $zipPath
    ReadmeIncluded = Test-Path -LiteralPath $releaseNotesPath
}

$result | Format-List
