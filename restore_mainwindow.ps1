git -C "C:\Users\conno\Documents\CloudBerry" checkout HEAD -- src/CloudBerry.App/MainWindow.xaml
if ($LASTEXITCODE -eq 0) {
    Write-Host "Restored from git HEAD"
} else {
    Write-Host "Git restore failed, exit: $LASTEXITCODE"
}
