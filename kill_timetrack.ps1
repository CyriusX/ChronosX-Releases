Get-Process | Where-Object { $_.ProcessName -like '*TimeTrack*' } | Stop-Process -Force
Write-Host "Killed TimeTrack processes"
