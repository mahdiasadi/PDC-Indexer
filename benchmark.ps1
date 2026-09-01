$outputFile = "E:\Test\ProjectIndexer\benchmark_result.txt"
Set-Location -LiteralPath "E:\Test\ProjectIndexer"

# Delete old index DB to get a clean benchmark
$dbPath = "$env:LOCALAPPDATA\ProjectIndexer\index.db"
if (Test-Path $dbPath) { Remove-Item -Force $dbPath }

# Create input: 'C' then Enter
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "dotnet"
$psi.Arguments = "run --project src\ProjectIndexer.Console"
$psi.WorkingDirectory = "E:\Test\ProjectIndexer"
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true

$p = [System.Diagnostics.Process]::Start($psi)
$p.StandardInput.WriteLine("C")
$p.StandardInput.Close()

$output = $p.StandardOutput.ReadToEnd()
$errorOutput = $p.StandardError.ReadToEnd()
$p.WaitForExit()

$output + $errorOutput | Out-File -FilePath $outputFile -Encoding UTF8
Write-Host "Benchmark complete. Output written to $outputFile"
