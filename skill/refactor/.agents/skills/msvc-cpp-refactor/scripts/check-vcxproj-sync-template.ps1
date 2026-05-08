[CmdletBinding(DefaultParameterSetName = 'ProjectFile')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'ProjectFile')]
    [string]$ProjectFile,

    [Parameter(Mandatory = $true, ParameterSetName = 'WorkspaceRoot')]
    [string]$WorkspaceRoot,

    [Parameter(Mandatory = $true, ParameterSetName = 'GitDiff')]
    [string]$GitDiffRange,

    [Parameter(ParameterSetName = 'GitDiff')]
    [string]$GitRoot = '.',

    [Parameter(ParameterSetName = 'ProjectFile')]
    [string]$FiltersFile,

    [string[]]$MovedPathPairs = @(),

    [switch]$FailOnIssue
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-FullPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaseDirectory,

        [Parameter(Mandatory = $true)]
        [string]$IncludePath
    )

    return [System.IO.Path]::GetFullPath((Join-Path -Path $BaseDirectory -ChildPath $IncludePath))
}

function Get-IncludeNodes {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath
    )

    [xml]$xml = Get-Content -Raw -Path $FilePath
    $namespaceUri = $xml.DocumentElement.NamespaceURI

    if ([string]::IsNullOrWhiteSpace($namespaceUri)) {
        return $xml.SelectNodes('//*[@Include]')
    }

    $namespaceManager = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    $namespaceManager.AddNamespace('msb', $namespaceUri)
    return $xml.SelectNodes('//*[@Include] | //msb:*[@Include]', $namespaceManager)
}

function Get-ProjectItems {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath
    )

    $baseDirectory = Split-Path -Path $FilePath -Parent
    $supportedTypes = @('ClCompile', 'ClInclude', 'ResourceCompile', 'None', 'CustomBuild')

    foreach ($node in Get-IncludeNodes -FilePath $FilePath) {
        if ($supportedTypes -notcontains $node.LocalName) {
            continue
        }

        $include = [string]$node.Include
        if ([string]::IsNullOrWhiteSpace($include)) {
            continue
        }

        [pscustomobject]@{
            ItemType = $node.LocalName
            Include  = $include
            FullPath = Resolve-FullPath -BaseDirectory $baseDirectory -IncludePath $include
        }
    }
}

function Get-NormalizedMap {
    param(
        [Parameter(Mandatory = $true)]
        [Object[]]$Items
    )

    $map = @{}
    foreach ($item in $Items) {
        $map[$item.FullPath.ToLowerInvariant()] = $item
    }
    return $map
}

function Add-Issue {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.Generic.List[string]]$IssueList,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    $IssueList.Add($Message)
    Write-Warning $Message
}

function Get-ProjectsFromGitDiff {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedGitRoot,

        [Parameter(Mandatory = $true)]
        [string]$GitDiffRange
    )

    $null = & git -C $ResolvedGitRoot rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Git repository not found: $ResolvedGitRoot"
    }

    $changedPaths = @(
        & git -C $ResolvedGitRoot diff --name-only --diff-filter=ACMR $GitDiffRange -- '*.vcxproj' '*.vcxproj.filters'
    )

    $projectFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($changedPath in $changedPaths) {
        if ([string]::IsNullOrWhiteSpace($changedPath)) {
            continue
        }

        $normalizedPath = $changedPath.Trim()
        if ($normalizedPath.EndsWith('.vcxproj.filters', [System.StringComparison]::OrdinalIgnoreCase)) {
            $normalizedPath = $normalizedPath.Substring(0, $normalizedPath.Length - '.filters'.Length)
        }

        if (-not $normalizedPath.EndsWith('.vcxproj', [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $projectFullPath = [System.IO.Path]::GetFullPath((Join-Path -Path $ResolvedGitRoot -ChildPath $normalizedPath))
        $null = $projectFiles.Add($projectFullPath)
    }

    return @($projectFiles)
}

function Test-ProjectSync {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedProjectFile,

        [string]$ResolvedFiltersFile,

        [string[]]$MovedPathPairs = @()
    )

    $issues = [System.Collections.Generic.List[string]]::new()

    if (-not (Test-Path -LiteralPath $ResolvedProjectFile)) {
        Add-Issue -IssueList $issues -Message "Project file not found: $ResolvedProjectFile"
        return [pscustomobject]@{
            ProjectFile = $ResolvedProjectFile
            FiltersFile = $ResolvedFiltersFile
            Issues      = $issues
        }
    }

    if (-not $ResolvedFiltersFile) {
        $ResolvedFiltersFile = "$ResolvedProjectFile.filters"
    }

    if (-not (Test-Path -LiteralPath $ResolvedFiltersFile)) {
        Add-Issue -IssueList $issues -Message "Filters file not found: $ResolvedFiltersFile"
        return [pscustomobject]@{
            ProjectFile = $ResolvedProjectFile
            FiltersFile = $ResolvedFiltersFile
            Issues      = $issues
        }
    }

    $projectItems = @(Get-ProjectItems -FilePath $ResolvedProjectFile)
    $filterItems = @(Get-ProjectItems -FilePath $ResolvedFiltersFile)

    $projectMap = Get-NormalizedMap -Items $projectItems
    $filterMap = Get-NormalizedMap -Items $filterItems

    foreach ($item in $projectItems) {
        if (-not (Test-Path -LiteralPath $item.FullPath)) {
            Add-Issue -IssueList $issues -Message "Missing on disk but still referenced in vcxproj: $($item.Include) [$ResolvedProjectFile]"
        }

        if (-not $filterMap.ContainsKey($item.FullPath.ToLowerInvariant())) {
            Add-Issue -IssueList $issues -Message "Present in vcxproj but missing from vcxproj.filters: $($item.Include) [$ResolvedProjectFile]"
        }
    }

    foreach ($item in $filterItems) {
        if (-not (Test-Path -LiteralPath $item.FullPath)) {
            Add-Issue -IssueList $issues -Message "Missing on disk but still referenced in vcxproj.filters: $($item.Include) [$ResolvedProjectFile]"
        }

        if (-not $projectMap.ContainsKey($item.FullPath.ToLowerInvariant())) {
            Add-Issue -IssueList $issues -Message "Present in vcxproj.filters but missing from vcxproj: $($item.Include) [$ResolvedProjectFile]"
        }
    }

    foreach ($pair in $MovedPathPairs) {
        $parts = $pair -split '=>', 2
        if ($parts.Count -ne 2) {
            Add-Issue -IssueList $issues -Message "Invalid moved path pair format: $pair [$ResolvedProjectFile]"
            continue
        }

        $projectDirectory = Split-Path -Path $ResolvedProjectFile -Parent
        $oldFullPath = Resolve-FullPath -BaseDirectory $projectDirectory -IncludePath $parts[0].Trim()
        $newFullPath = Resolve-FullPath -BaseDirectory $projectDirectory -IncludePath $parts[1].Trim()

        if ($projectMap.ContainsKey($oldFullPath.ToLowerInvariant()) -or $filterMap.ContainsKey($oldFullPath.ToLowerInvariant())) {
            Add-Issue -IssueList $issues -Message "Old moved path still referenced: $($parts[0].Trim()) [$ResolvedProjectFile]"
        }

        if (-not $projectMap.ContainsKey($newFullPath.ToLowerInvariant())) {
            Add-Issue -IssueList $issues -Message "New moved path missing from vcxproj: $($parts[1].Trim()) [$ResolvedProjectFile]"
        }

        if (-not $filterMap.ContainsKey($newFullPath.ToLowerInvariant())) {
            Add-Issue -IssueList $issues -Message "New moved path missing from vcxproj.filters: $($parts[1].Trim()) [$ResolvedProjectFile]"
        }
    }

    return [pscustomobject]@{
        ProjectFile = $ResolvedProjectFile
        FiltersFile = $ResolvedFiltersFile
        Issues      = $issues
    }
}

function Write-ResultSummary {
    param(
        [Parameter(Mandatory = $true)]
        [Object]$Result
    )

    if ($Result.Issues.Count -eq 0) {
        Write-Host "VCXPROJ sync check passed: $($Result.ProjectFile)" -ForegroundColor Green
        return
    }

    Write-Host "VCXPROJ sync check found $($Result.Issues.Count) issue(s): $($Result.ProjectFile)" -ForegroundColor Yellow
}

if ($PSCmdlet.ParameterSetName -eq 'ProjectFile') {
    $result = Test-ProjectSync -ResolvedProjectFile $ProjectFile -ResolvedFiltersFile $FiltersFile -MovedPathPairs $MovedPathPairs
    Write-ResultSummary -Result $result

    if ($result.Issues.Count -gt 0 -and $FailOnIssue) {
        exit 1
    }

    exit 0
}

if ($PSCmdlet.ParameterSetName -eq 'GitDiff') {
    $resolvedGitRoot = Resolve-Path -LiteralPath $GitRoot -ErrorAction Stop
    $diffProjects = @(Get-ProjectsFromGitDiff -ResolvedGitRoot $resolvedGitRoot -GitDiffRange $GitDiffRange)

    if ($diffProjects.Count -eq 0) {
        Write-Host "No .vcxproj changes found in git diff range: $GitDiffRange" -ForegroundColor Cyan
        exit 0
    }

    $allResults = @()
    foreach ($diffProject in $diffProjects) {
        $result = Test-ProjectSync -ResolvedProjectFile $diffProject -MovedPathPairs $MovedPathPairs
        $allResults += $result
        Write-ResultSummary -Result $result
    }

    $issueCount = ($allResults | ForEach-Object { $_.Issues.Count } | Measure-Object -Sum).Sum
    if (-not $issueCount) {
        Write-Host "Git diff VCXPROJ sync check passed across $($diffProjects.Count) project(s)." -ForegroundColor Green
        exit 0
    }

    Write-Host "Git diff VCXPROJ sync check found $issueCount issue(s) across $($diffProjects.Count) project(s)." -ForegroundColor Yellow

    if ($FailOnIssue) {
        exit 1
    }

    exit 0
}

$resolvedWorkspaceRoot = Resolve-Path -LiteralPath $WorkspaceRoot -ErrorAction Stop
$workspaceProjects = @(Get-ChildItem -Path $resolvedWorkspaceRoot -Filter '*.vcxproj' -File -Recurse)

if ($workspaceProjects.Count -eq 0) {
    Write-Host "No .vcxproj files found under workspace root: $resolvedWorkspaceRoot" -ForegroundColor Cyan
    exit 0
}

$allResults = @()
foreach ($workspaceProject in $workspaceProjects) {
    $result = Test-ProjectSync -ResolvedProjectFile $workspaceProject.FullName -MovedPathPairs $MovedPathPairs
    $allResults += $result
    Write-ResultSummary -Result $result
}

$issueCount = ($allResults | ForEach-Object { $_.Issues.Count } | Measure-Object -Sum).Sum
if (-not $issueCount) {
    Write-Host "Workspace VCXPROJ sync check passed across $($workspaceProjects.Count) project(s)." -ForegroundColor Green
    exit 0
}

Write-Host "Workspace VCXPROJ sync check found $issueCount issue(s) across $($workspaceProjects.Count) project(s)." -ForegroundColor Yellow

if ($FailOnIssue) {
    exit 1
}

exit 0