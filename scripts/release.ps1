param (
    [string]$VersionType = "patch", # patch, minor, major
    [string]$CustomVersion = $null,
    [string]$CommitMessage = "release: {version}"
)

# 1. Get current version from AltKey.csproj
$csprojPath = "AltKey/AltKey.csproj"
if (-not (Test-Path $csprojPath)) {
    Write-Error "Could not find $csprojPath"
    exit 1
}

[xml]$csproj = Get-Content $csprojPath
$currentVersionStr = $csproj.Project.PropertyGroup.Version
$currentAssemblyVersionStr = $csproj.Project.PropertyGroup.AssemblyVersion

Write-Host "Current version: $currentVersionStr"

# 2. Determine new version
if ($CustomVersion) {
    $newVersionStr = $CustomVersion
} else {
    $v = [version]$currentVersionStr
    if ($VersionType -eq "major") {
        $newVersionStr = "$($v.Major + 1).0.0"
    } elseif ($VersionType -eq "minor") {
        $newVersionStr = "$($v.Major).$($v.Minor + 1).0"
    } else {
        $newVersionStr = "$($v.Major).$($v.Minor).$($v.Build + 1)"
    }
}

$newAssemblyVersionStr = "$newVersionStr.0"
Write-Host "New version: $newVersionStr (AssemblyVersion: $newAssemblyVersionStr)"

# 3. Update AltKey.csproj
$csproj.Project.PropertyGroup.Version = $newVersionStr
$csproj.Project.PropertyGroup.AssemblyVersion = $newAssemblyVersionStr
$csproj.Project.PropertyGroup.FileVersion = $newAssemblyVersionStr
$csproj.Save($csprojPath)

Write-Host "Updated $csprojPath"

# 4. Git operations
Write-Host "Summarizing changes..."
$prevTag = git describe --tags --abbrev=0 2>$null
$summary = ""

if ($prevTag) {
    $commits = git log "$prevTag..HEAD" --oneline
    if ($commits) {
        $summary += "`n`nCommits since last release:`n" + ($commits -join "`n")
    }
}

$changes = git status --short
if ($changes) {
    $summary += "`n`nFiles in this release:`n" + ($changes -join "`n")
}

$finalCommitMessage = $CommitMessage.Replace("{version}", "v$newVersionStr") + $summary

Write-Host "Committing changes..."
git add .
git commit -m $finalCommitMessage

Write-Host "Tagging v$newVersionStr..."
git tag "v$newVersionStr"

Write-Host "Pushing to origin..."
git push origin main
git push origin --tags

Write-Host "Successfully released v$newVersionStr!"
