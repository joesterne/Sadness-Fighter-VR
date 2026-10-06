param([string]$Command = 'editor_status', [string]$Parameters = '{}', [string]$ParametersFile = '', [switch]$Job)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$descriptor = Get-Content (Join-Path $project 'Library/Pipeline/.unity-pipeline-port') -Raw | ConvertFrom-Json
if ($ParametersFile) { $Parameters = Get-Content $ParametersFile -Raw }
$body = @{ command = $Command; parameters = ($Parameters | ConvertFrom-Json); job = [bool]$Job } | ConvertTo-Json -Depth 30 -Compress
Invoke-RestMethod -Uri "http://127.0.0.1:$($descriptor.port)/api/exec" -Method Post -Headers @{Authorization="Bearer $($descriptor.evalToken)"} -ContentType 'application/json' -Body $body -NoProxy -TimeoutSec 120 | ConvertTo-Json -Depth 35
