param(
    [string]$InputFile,

    [switch]$Unique
)

# Read any piped input up front.
$pipedObjects = @($input)

# Read input from file, pipeline, or clipboard.
if ($InputFile) {
    if (-not (Test-Path -LiteralPath $InputFile)) {
        Write-Error "Input file not found: $InputFile"
        exit 1
    }
    $text = Get-Content -LiteralPath $InputFile -Raw
}
elseif ($pipedObjects.Count -gt 0) {
    $text = ($pipedObjects | ForEach-Object { [string]$_ }) -join [Environment]::NewLine
}
elseif (-not [Console]::IsInputRedirected) {
    try {
        $text = Get-Clipboard -Raw
    }
    catch {
        Write-Error "No input file/pipeline detected and clipboard is unavailable."
        exit 1
    }
}
else {
    $text = [Console]::In.ReadToEnd()
}

$matches = [regex]::Matches($text, '(?im)^\s*(TS-[^\r\n]+?)\s*$')
$results = foreach ($m in $matches) { $m.Groups[1].Value.Trim() }

if ($Unique) {
    $results = $results | Select-Object -Unique
}

$results | ForEach-Object { Write-Output $_ }
