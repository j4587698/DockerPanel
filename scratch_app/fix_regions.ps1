$files = Get-ChildItem -Path "Backend\DockerPanel.API\Services\Acme\CertesAcmeService*.cs"
foreach ($file in $files) {
    $content = Get-Content -Path $file.FullName
    $newContent = $content | Where-Object { $_ -notmatch '^\s*#(end)?region' }
    Set-Content -Path $file.FullName -Value $newContent
}
