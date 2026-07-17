$files = Get-ChildItem -Path Backend\DockerPanel.API\Services -Recurse -Filter *.cs
foreach ($file in $files) {
    if ($file.Name -eq "DbCollections.cs") { continue }
    $content = Get-Content $file.FullName -Raw
    $original = $content
    
    $content = $content.Replace('"operation_audit_logs"', 'DbCollections.OperationAudits')
    $content = $content.Replace('"resource_alert_rules"', 'DbCollections.ResourceAlertRules')
    $content = $content.Replace('"certificates"', 'DbCollections.Certificates')
    $content = $content.Replace('"certificate_operations"', 'DbCollections.CertificateOperations')
    $content = $content.Replace('"acme_accounts"', 'DbCollections.AcmeAccounts')
    $content = $content.Replace('"acme_orders"', 'DbCollections.AcmeOrders')
    $content = $content.Replace('"acme_operation_logs"', 'DbCollections.AcmeOperationLogs')
    $content = $content.Replace('"acme_jobs"', 'DbCollections.AcmeJobs')
    $content = $content.Replace('UsersCollectionName = "users"', 'UsersCollectionName = DbCollections.Users')
    
    if ($content -ne $original) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        Write-Host "Updated $($file.Name)"
    }
}
