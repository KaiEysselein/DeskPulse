param(
    [Parameter(Mandatory = $true)]
    [string]$TrayPath,

    [string]$ErrorLogPath = ''
)

$ErrorActionPreference = 'Stop'
$taskName = 'DeskPulse Tray'
$usersSid = 'S-1-5-32-545'

if (!(Test-Path -LiteralPath $TrayPath -PathType Leaf)) {
    throw "DeskPulse Tray was not found at: $TrayPath"
}

try {
    $action = New-ScheduledTaskAction -Execute $TrayPath
    $trigger = New-ScheduledTaskTrigger -AtLogOn
    $principal = New-ScheduledTaskPrincipal -GroupId $usersSid -RunLevel Limited
    $settings = New-ScheduledTaskSettingsSet `
        -MultipleInstances Parallel `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries `
        -ExecutionTimeLimit ([TimeSpan]::Zero)

    Register-ScheduledTask `
        -TaskName $taskName `
        -Action $action `
        -Trigger $trigger `
        -Principal $principal `
        -Settings $settings `
        -Description 'Starts one unelevated DeskPulse tray instance in every interactive user session.' `
        -Force | Out-Null

    if (![string]::IsNullOrWhiteSpace($ErrorLogPath) -and
        (Test-Path -LiteralPath $ErrorLogPath)) {
        Remove-Item -LiteralPath $ErrorLogPath -Force
    }
}
catch {
    if (![string]::IsNullOrWhiteSpace($ErrorLogPath)) {
        $_ | Out-String | Set-Content -LiteralPath $ErrorLogPath -Encoding UTF8
    }
    throw
}
