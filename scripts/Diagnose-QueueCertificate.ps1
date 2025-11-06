# Diagnostic Script for Azure Queue Certificate Authentication
# Run this script on the server where the API is deployed

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Azure Queue Certificate Diagnostics" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$thumbprint = "61FC110D5BABD61419B106862B304C2FFF57A262"
$clientId = "c8034569-4990-4823-9f1d-b46223789c35"
$tenantId = "d6dbad84-5922-4700-a049-c7068c37c884"
$appPoolIdentity = "IIS APPPOOL\SecureBootDashboard.Api"

# 1. Check Certificate Exists
Write-Host "[1/7] Checking certificate in LocalMachine\My store..." -ForegroundColor Yellow
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object {
    $_.Thumbprint -eq $thumbprint
}

if ($cert) {
    Write-Host "  ? Certificate found" -ForegroundColor Green
    Write-Host "     Subject: $($cert.Subject)" -ForegroundColor Gray
    Write-Host "     Issuer: $($cert.Issuer)" -ForegroundColor Gray
    Write-Host "     Valid from: $($cert.NotBefore)" -ForegroundColor Gray
    Write-Host "     Valid until: $($cert.NotAfter)" -ForegroundColor Gray
    
    # Check expiration
    if ($cert.NotAfter -lt (Get-Date)) {
        Write-Host "  ? CERTIFICATE EXPIRED!" -ForegroundColor Red
        Write-Host "     The certificate expired on: $($cert.NotAfter)" -ForegroundColor Red
    } elseif ($cert.NotAfter -lt (Get-Date).AddDays(30)) {
        Write-Host "  ??  Certificate expires soon: $($cert.NotAfter)" -ForegroundColor Yellow
    } else {
        Write-Host "  ? Certificate is valid" -ForegroundColor Green
    }
} else {
    Write-Host "  ? Certificate NOT found!" -ForegroundColor Red
    Write-Host "     Thumbprint: $thumbprint" -ForegroundColor Red
    Write-Host ""
    Write-Host "  ACTION REQUIRED:" -ForegroundColor Yellow
    Write-Host "  1. Import the certificate to LocalMachine\My store" -ForegroundColor White
    Write-Host "  2. Ensure certificate has private key" -ForegroundColor White
    exit 1
}
Write-Host ""

# 2. Check Private Key
Write-Host "[2/7] Checking private key..." -ForegroundColor Yellow
if ($cert.HasPrivateKey) {
    Write-Host "  ? Certificate has private key" -ForegroundColor Green
    
    try {
        $rsaCert = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
        $keyName = $rsaCert.Key.UniqueName
        $keyPath = "C:\ProgramData\Microsoft\Crypto\Keys\$keyName"
        
        Write-Host "     Key path: $keyPath" -ForegroundColor Gray
        
        if (Test-Path $keyPath) {
            Write-Host "  ? Private key file exists" -ForegroundColor Green
        } else {
            Write-Host "  ??  Private key file not found at expected location" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  ??  Could not access private key: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ? Certificate does NOT have private key!" -ForegroundColor Red
    Write-Host ""
    Write-Host "  ACTION REQUIRED:" -ForegroundColor Yellow
    Write-Host "  1. Re-import certificate with private key (PFX format)" -ForegroundColor White
    Write-Host "  2. Use 'Mark this key as exportable' option during import" -ForegroundColor White
    exit 1
}
Write-Host ""

# 3. Check Private Key Permissions
Write-Host "[3/7] Checking private key permissions..." -ForegroundColor Yellow
try {
    $rsaCert = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
    $keyName = $rsaCert.Key.UniqueName
    $keyPath = "C:\ProgramData\Microsoft\Crypto\Keys\$keyName"
    
    if (Test-Path $keyPath) {
        $acl = Get-Acl $keyPath
        $hasAppPoolAccess = $acl.Access | Where-Object {
            $_.IdentityReference -like "*$appPoolIdentity*" -or
            $_.IdentityReference -like "*IIS_IUSRS*" -or
            $_.IdentityReference -like "*NETWORK SERVICE*"
        }
        
        if ($hasAppPoolAccess) {
            Write-Host "  ? Application Pool has access to private key" -ForegroundColor Green
            foreach ($access in $hasAppPoolAccess) {
                Write-Host "     - $($access.IdentityReference): $($access.FileSystemRights)" -ForegroundColor Gray
            }
        } else {
            Write-Host "  ? Application Pool does NOT have access to private key!" -ForegroundColor Red
            Write-Host ""
            Write-Host "  ACTION REQUIRED:" -ForegroundColor Yellow
            Write-Host "  Run this command to grant access:" -ForegroundColor White
            Write-Host "  icacls `"$keyPath`" /grant `"${appPoolIdentity}:R`"" -ForegroundColor Cyan
        }
    }
} catch {
    Write-Host "  ??  Could not check permissions: $_" -ForegroundColor Yellow
}
Write-Host ""

# 4. Check Azure CLI installed
Write-Host "[4/7] Checking Azure CLI..." -ForegroundColor Yellow
try {
    $azVersion = az version --output json 2>$null | ConvertFrom-Json
    if ($azVersion) {
        Write-Host "  ? Azure CLI installed (version: $($azVersion.'azure-cli'))" -ForegroundColor Green
    }
} catch {
    Write-Host "  ??  Azure CLI not installed or not in PATH" -ForegroundColor Yellow
    Write-Host "     Cannot check Azure AD configuration" -ForegroundColor Gray
    Write-Host "     Download from: https://aka.ms/installazurecliwindows" -ForegroundColor Gray
}
Write-Host ""

# 5. Check if logged into Azure
Write-Host "[5/7] Checking Azure authentication..." -ForegroundColor Yellow
try {
    $account = az account show 2>$null | ConvertFrom-Json
    if ($account) {
        Write-Host "  ? Logged into Azure" -ForegroundColor Green
        Write-Host "     Subscription: $($account.name)" -ForegroundColor Gray
        Write-Host "     Tenant: $($account.tenantId)" -ForegroundColor Gray
        
        if ($account.tenantId -ne $tenantId) {
            Write-Host "  ??  WARNING: Logged into different tenant!" -ForegroundColor Yellow
            Write-Host "     Expected: $tenantId" -ForegroundColor Yellow
            Write-Host "     Current: $($account.tenantId)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  ??  Not logged into Azure CLI" -ForegroundColor Yellow
        Write-Host "     Cannot check App Registration configuration" -ForegroundColor Gray
        Write-Host "     Run: az login --tenant $tenantId" -ForegroundColor Cyan
    }
} catch {
    Write-Host "  ??  Not logged into Azure CLI" -ForegroundColor Yellow
}
Write-Host ""

# 6. Check App Registration (if logged in)
Write-Host "[6/7] Checking Azure AD App Registration..." -ForegroundColor Yellow
try {
    $sp = az ad sp list --filter "appId eq '$clientId'" 2>$null | ConvertFrom-Json
    if ($sp) {
        Write-Host "  ? App Registration found" -ForegroundColor Green
        Write-Host "     Display Name: $($sp[0].displayName)" -ForegroundColor Gray
        Write-Host "     Object ID: $($sp[0].id)" -ForegroundColor Gray
        
        # Check if certificate is uploaded
        Write-Host "     Checking uploaded certificates..." -ForegroundColor Gray
        $app = az ad app show --id $clientId 2>$null | ConvertFrom-Json
        if ($app.keyCredentials) {
            $matchingCert = $app.keyCredentials | Where-Object {
                $_.customKeyIdentifier -like "*$($thumbprint.Replace(':', ''))*"
            }
            
            if ($matchingCert) {
                Write-Host "  ? Certificate uploaded to App Registration" -ForegroundColor Green
            } else {
                Write-Host "  ? Certificate NOT found in App Registration!" -ForegroundColor Red
                Write-Host ""
                Write-Host "  ACTION REQUIRED:" -ForegroundColor Yellow
                Write-Host "  1. Export certificate (public key only):" -ForegroundColor White
                Write-Host "     Export-Certificate -Cert (Get-Item Cert:\LocalMachine\My\$thumbprint) -FilePath SecureBootCert.cer" -ForegroundColor Cyan
                Write-Host "  2. Upload to Azure Portal:" -ForegroundColor White
                Write-Host "     Azure AD ? App registrations ? Your App ? Certificates & secrets ? Upload certificate" -ForegroundColor Cyan
            }
        }
    } else {
        Write-Host "  ? App Registration NOT found!" -ForegroundColor Red
        Write-Host "     Client ID: $clientId" -ForegroundColor Red
    }
} catch {
    Write-Host "  ??  Could not check App Registration (not logged in or insufficient permissions)" -ForegroundColor Yellow
}
Write-Host ""

# 7. Check Storage Queue access
Write-Host "[7/7] Checking Storage Queue permissions..." -ForegroundColor Yellow
try {
    $storageAccount = "secbootcert"
    $queueName = "secureboot-reports"
    
    # Try to get queue
    $queue = az storage queue exists --name $queueName --account-name $storageAccount --auth-mode login 2>$null | ConvertFrom-Json
    
    if ($queue.exists) {
        Write-Host "  ? Queue exists: $queueName" -ForegroundColor Green
        
        # Check role assignments
        Write-Host "     Checking role assignments..." -ForegroundColor Gray
        $sp = az ad sp list --filter "appId eq '$clientId'" 2>$null | ConvertFrom-Json
        if ($sp) {
            $objectId = $sp[0].id
            $roles = az role assignment list --assignee $objectId --scope "/subscriptions/*/resourceGroups/*/providers/Microsoft.Storage/storageAccounts/$storageAccount" 2>$null | ConvertFrom-Json
            
            $hasQueueRole = $roles | Where-Object {
                $_.roleDefinitionName -like "*Queue*"
            }
            
            if ($hasQueueRole) {
                Write-Host "  ? App has Storage Queue role assignment" -ForegroundColor Green
                foreach ($role in $hasQueueRole) {
                    Write-Host "     - $($role.roleDefinitionName)" -ForegroundColor Gray
                }
            } else {
                Write-Host "  ? App does NOT have Storage Queue role!" -ForegroundColor Red
                Write-Host ""
                Write-Host "  ACTION REQUIRED:" -ForegroundColor Yellow
                Write-Host "  Assign 'Storage Queue Data Contributor' role:" -ForegroundColor White
                Write-Host "  az role assignment create --role 'Storage Queue Data Contributor' --assignee $objectId --scope <storage-account-resource-id>" -ForegroundColor Cyan
            }
        }
    } else {
        Write-Host "  ??  Queue does not exist or no access" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ??  Could not check Storage Queue (not logged in or insufficient permissions)" -ForegroundColor Yellow
}
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Diagnostic Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Review the results above and address any ? or ??  items." -ForegroundColor White
Write-Host ""
Write-Host "If all checks pass, try restarting the API:" -ForegroundColor White
Write-Host "  iisreset" -ForegroundColor Cyan
Write-Host ""
