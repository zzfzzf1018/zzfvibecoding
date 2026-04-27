param(
    [string]$File1 = ".\1.txt",
    [string]$File2 = ".\2.txt",
    [string]$OutputFile = ".\output.txt"
)

if (-not (Test-Path -Path $File1)) {
    throw "文件不存在: $File1"
}

if (-not (Test-Path -Path $File2)) {
    throw "文件不存在: $File2"
}

$lines1 = Get-Content -Path $File1 | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$lines2 = Get-Content -Path $File2 | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

$onlyInFile1 = $lines1 | Where-Object { $_ -notin $lines2 } | Sort-Object -Unique
$onlyInFile2 = $lines2 | Where-Object { $_ -notin $lines1 } | Sort-Object -Unique

$result = New-Object System.Collections.Generic.List[string]
$result.Add("比较文件: $File1 <-> $File2")
$result.Add("生成时间: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$result.Add("")

$result.Add("只在第一个文件中存在的数据:")
if ($onlyInFile1.Count -gt 0) {
    $onlyInFile1 | ForEach-Object { $result.Add($_) }
} else {
    $result.Add("无")
}

$result.Add("")
$result.Add("只在第二个文件中存在的数据:")
if ($onlyInFile2.Count -gt 0) {
    $onlyInFile2 | ForEach-Object { $result.Add($_) }
} else {
    $result.Add("无")
}

$result | Set-Content -Path $OutputFile -Encoding UTF8

Write-Host "对比完成，结果已输出到: $OutputFile"