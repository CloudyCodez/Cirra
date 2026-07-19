# Quick check: does the installed exe contain our recent additions?
$bytes = [System.IO.File]::ReadAllBytes("C:\Users\conno\AppData\Local\Programs\CloudBerry\CloudBerry.exe")
$text  = [System.Text.Encoding]::UTF8.GetString($bytes)

$checks = @(
    "StartupView",
    "StartupViewIsCloudscape", 
    "IsCloudscapeCompact",
    "SchedulePersist",
    "_heroAdvanceTimer",
    "AnimateShelvesFadeIn",
    "TrailerPreviewTimer",
    "localhost",
    "_trailerServerPort"
)

foreach ($c in $checks) {
    $idx = $text.IndexOf($c)
    Write-Host ("{0,-30} {1}" -f $c, $(if ($idx -ge 0) { "FOUND at $idx" } else { "NOT FOUND" }))
}
