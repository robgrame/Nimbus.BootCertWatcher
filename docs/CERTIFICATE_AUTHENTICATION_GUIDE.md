# Certificate Authentication Configuration Guide

## Overview

This guide explains how to configure **mutual TLS (mTLS) authentication** for the Secure Boot Report Proxy Azure Function in a **multi-tier PKI environment** with multiple Issuing CAs. Certificate authentication provides defense-in-depth security by validating client identity through X.509 certificates in addition to API key authentication.

---

## Table of Contents

1. [PKI Architecture Overview](#pki-architecture-overview)
2. [Certificate Validation Layers](#certificate-validation-layers)
3. [Configuration Parameters](#configuration-parameters)
4. [Multi-Tier PKI Setup](#multi-tier-pki-setup)
5. [Step-by-Step Configuration](#step-by-step-configuration)
6. [Client Configuration](#client-configuration)
7. [Testing & Validation](#testing--validation)
8. [Troubleshooting](#troubleshooting)
9. [Best Practices](#best-practices)
10. [Security Considerations](#security-considerations)

---

## PKI Architecture Overview

### Typical Enterprise PKI Hierarchy

```
┌─────────────────────────────────────┐
│   Root CA (Offline, Secure)         │
│   CN=Contoso Root CA                │
│   Validity: 20 years                │
└─────────────────┬───────────────────┘
                  │
     ┌────────────┼────────────┬──────────────┐
     │            │            │              │
┌────▼─────┐ ┌───▼──────┐ ┌──▼───────┐ ┌────▼─────┐
│Issuing   │ │Issuing   │ │Issuing   │ │Issuing   │
│CA 01     │ │CA 02     │ │CA 03     │ │CA 04     │
│(Devices) │ │(Servers) │ │(Users)   │ │(IoT)     │
│Validity: │ │Validity: │ │Validity: │ │Validity: │
│5 years   │ │5 years   │ │3 years   │ │5 years   │
└────┬─────┘ └───┬──────┘ └──┬───────┘ └────┬─────┘
     │           │            │              │
  ┌──▼──┐     ┌─▼──┐      ┌──▼──┐        ┌──▼──┐
  │Dev  │     │Web │      │User │        │IoT  │
  │Certs│     │Svr │      │Certs│        │Certs│
  └─────┘     └────┘      └─────┘        └─────┘
```

### Certificate Chain Example

When a client presents a certificate, the **certificate chain** is validated:

```
┌──────────────────────────────────────┐
│ 1. End-Entity Certificate (Leaf)    │ ← Client certificate
│    CN=DESKTOP-12345.contoso.com      │
│    Issued by: Issuing CA 01          │
│    Thumbprint: ABC123DEF...          │
└──────────────┬───────────────────────┘
               │
┌──────────────▼───────────────────────┐
│ 2. Subordinate CA (Intermediate)     │ ← Issuing CA
│    CN=Contoso Device Management CA   │
│    Issued by: Root CA                │
│    Thumbprint: 789GHI012...          │
└──────────────┬───────────────────────┘
               │
┌──────────────▼───────────────────────┐
│ 3. Root CA                           │ ← Trusted Root
│    CN=Contoso Root CA                │
│    Self-signed                       │
│    Thumbprint: XYZ456ABC...          │
└──────────────────────────────────────┘
```

---

## Certificate Validation Layers

The Azure Function performs **four layers** of certificate validation:

### Layer 1: Basic Certificate Validation ✅
- **Expiration check**: Certificate is within validity period (NotBefore ≤ Now ≤ NotAfter)
- **Revocation check** (optional): Certificate is not revoked (CRL/OCSP)
- **Chain building**: Certificate chains to a trusted Root CA

### Layer 2: Root CA Validation 🔐
- **Root CA Subject validation**: Ensures chain terminates at expected Root CA
- **Root CA Thumbprint validation**: Verifies Root CA fingerprint

### Layer 3: Subordinate CA Validation 🏢
- **Intermediate CA presence**: Validates expected Issuing CAs are in chain
- **Issuing CA Subject matching**: Ensures certificate issued by specific Subordinate CA
- **Issuing CA Thumbprint matching**: Verifies Subordinate CA fingerprint

### Layer 4: Client Certificate Allowlist 📋
- **Thumbprint allowlist**: Only specific client certificates are accepted
- **Explicit authorization**: Granular control per device/client

---

## Configuration Parameters

### Environment Variables for Certificate Authentication

| Variable | Type | Required | Description |
|----------|------|----------|-------------|
| `RequireCertificateAuthentication` | bool | Yes | Enable mutual TLS authentication |
| `CertificateThumbprints` | string | No* | Comma-separated client cert thumbprints allowlist |
| `CertificateValidateExpiration` | bool | No | Validate cert expiration (default: true) |
| `CertificateValidateChain` | bool | No | Validate cert chain to root (default: true) |
| `CertificateCheckRevocation` | bool | No | Check CRL for revocation (default: false) |
| `CertificateExpectedCARootName` | string | No** | Expected Root CA Subject name |
| `CertificateExpectedCARootThumbprint` | string | No** | Expected Root CA thumbprint |
| `CertificateExpectedSubordinateCAsJson` | JSON | No** | Expected Subordinate CAs (JSON array) |

**Notes:**
- *If `CertificateThumbprints` is empty, any valid certificate is accepted (not recommended for production)
- **At least one CA validation parameter should be configured for production security

### Subordinate CA JSON Format

```json
[
  {
    "name": "CN=Contoso Device Management CA, O=Contoso, C=US",
    "thumbprint": "ABC123DEF456789ABCDEF123456789ABCDEF1234"
  },
  {
    "name": "CN=Contoso IoT Issuing CA, O=Contoso, C=US",
    "thumbprint": "789GHI012JKL345MNO678PQR901STU234VWX567"
  }
]
```

**Field Requirements:**
- `name`: Subject Distinguished Name (DN) of the Subordinate CA
- `thumbprint`: SHA-1 fingerprint (40 hex characters, no spaces/colons)
- Both fields are optional, but at least one should be specified
- Matching logic: `(name matches OR name is empty) AND (thumbprint matches OR thumbprint is empty)`

---

## Multi-Tier PKI Setup

### Scenario: Multiple Issuing CAs for Different Certificate Types

**Business Requirement:**
- Devices managed by MDM should use certificates from "Device Management CA"
- IoT devices should use certificates from "IoT Issuing CA"
- Azure Function should **only accept device certificates**, not IoT certificates

### PKI Structure

```
Contoso Root CA
├── Contoso Device Management CA (for managed devices) ← ✅ Accept
├── Contoso IoT Issuing CA (for IoT sensors) ← ❌ Reject
├── Contoso User CA (for user authentication) ← ❌ Reject
└── Contoso Server CA (for web servers) ← ❌ Reject
```

### Configuration Strategy

**Option A: Subordinate CA Validation Only**
- Accept any certificate issued by "Device Management CA"
- Automatically accepts new devices without updating Function config
- Suitable for: Dynamic device enrollment scenarios

**Option B: Subordinate CA + Client Thumbprint Allowlist**
- Accept certificates from "Device Management CA" **AND** in allowlist
- Explicit per-device authorization
- Suitable for: High-security environments with static device inventory

**Option C: Full Stack Validation**
- Root CA + Subordinate CA + Client Thumbprint
- Maximum security, defense-in-depth
- Suitable for: Critical infrastructure, compliance requirements

---

## Step-by-Step Configuration

### Step 1: Extract Certificate Information

#### 1.1 Export Root CA Certificate

**From Windows Certificate Store:**
```powershell
# Open Certificate Manager
certmgr.msc

# Navigate to: Trusted Root Certification Authorities → Certificates
# Right-click Root CA → All Tasks → Export...
# Save as: contoso-root-ca.cer
```

#### 1.2 Extract Root CA Details

**Using PowerShell:**
```powershell
# Load certificate
$rootCert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2("C:\Temp\contoso-root-ca.cer")

# Extract Subject name
$rootCert.Subject
# Output: CN=Contoso Root CA, O=Contoso Corporation, C=US

# Extract thumbprint
$rootCert.Thumbprint
# Output: A1B2C3D4E5F6G7H8I9J0K1L2M3N4O5P6Q7R8S9T0
```

**Using OpenSSL (Linux/macOS):**
```bash
# Extract Subject
openssl x509 -in contoso-root-ca.cer -noout -subject
# Output: subject=CN=Contoso Root CA, O=Contoso Corporation, C=US

# Extract thumbprint (SHA-1)
openssl x509 -in contoso-root-ca.cer -noout -fingerprint -sha1
# Output: SHA1 Fingerprint=A1:B2:C3:D4:E5:F6:G7:H8:I9:J0:K1:L2:M3:N4:O5:P6:Q7:R8:S9:T0
```

#### 1.3 Export Subordinate CA Certificates

Repeat the same process for each Issuing CA:

```powershell
# Example: Device Management CA
$issuingCert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2("C:\Temp\device-management-ca.cer")

# Subject
$issuingCert.Subject
# Output: CN=Contoso Device Management CA, O=Contoso, C=US

# Thumbprint
$issuingCert.Thumbprint
# Output: 789ABC012DEF345GHI678JKL901MNO234PQR567
```

#### 1.4 Extract Client Certificate Thumbprints

**From device (PowerShell as Administrator):**
```powershell
# List all client certificates with private key
Get-ChildItem -Path Cert:\LocalMachine\My | Where-Object { $_.HasPrivateKey } | 
    Select-Object Subject, Thumbprint, NotAfter, Issuer | Format-List

# Example output:
# Subject    : CN=DESKTOP-12345.contoso.com
# Thumbprint : ABC123DEF456789012345678901234567890ABCD
# NotAfter   : 12/15/2025 10:30:00 AM
# Issuer     : CN=Contoso Device Management CA, O=Contoso, C=US
```

---

### Step 2: Build Configuration JSON

#### 2.1 Create Subordinate CAs JSON

**Template:**
```json
[
  {
    "name": "<Subordinate CA Subject DN>",
    "thumbprint": "<Subordinate CA SHA-1 Thumbprint>"
  }
]
```

**Example (Single Issuing CA):**
```json
[
  {
    "name": "CN=Contoso Device Management CA, O=Contoso, C=US",
    "thumbprint": "789ABC012DEF345GHI678JKL901MNO234PQR567"
  }
]
```

**Example (Multiple Issuing CAs):**
```json
[
  {
    "name": "CN=Contoso Device Management CA 01, O=Contoso, C=US",
    "thumbprint": "789ABC012DEF345GHI678JKL901MNO234PQR567"
  },
  {
    "name": "CN=Contoso Device Management CA 02, O=Contoso, C=US",
    "thumbprint": "890DEF123GHI456JKL789MNO012PQR345STU678"
  }
]
```

**Note:** Remove spaces and colons from thumbprints!

#### 2.2 Minify JSON for Environment Variable

**PowerShell:**
```powershell
# Minify JSON (remove whitespace)
$json = @"
[
  {
    "name": "CN=Contoso Device Management CA, O=Contoso, C=US",
    "thumbprint": "789ABC012DEF345GHI678JKL901MNO234PQR567"
  }
]
"@

$minified = ($json | ConvertFrom-Json | ConvertTo-Json -Compress)
Write-Output $minified
# Output: [{"name":"CN=Contoso Device Management CA, O=Contoso, C=US","thumbprint":"789ABC012DEF345GHI678JKL901MNO234PQR567"}]
```

---

### Step 3: Configure Azure Function

#### 3.1 Enable Mutual TLS in Azure App Service

**Azure Portal:**
1. Navigate to your Function App
2. Go to **Configuration** → **General settings**
3. Set **Client certificate mode** to **Optional** or **Required**
   - **Optional**: Function code validates certificate (allows HTTP health checks)
   - **Required**: App Service enforces certificate (blocks requests without cert)
4. Click **Save**

**Azure CLI:**
```bash
az functionapp update \
  --name <function-app-name> \
  --resource-group <resource-group> \
  --set clientCertEnabled=true \
  --set clientCertMode=Optional
```

**Recommendation:** Use `Optional` mode for better logging and control. The Function code will validate certificates and reject invalid ones.

#### 3.2 Add Application Settings

**Azure Portal:**
1. Navigate to **Configuration** → **Application settings**
2. Add the following settings:

| Name | Value | Example |
|------|-------|---------|
| `RequireCertificateAuthentication` | `true` | `true` |
| `CertificateValidateChain` | `true` | `true` |
| `CertificateValidateExpiration` | `true` | `true` |
| `CertificateCheckRevocation` | `false` | `false` (⚠️ can cause delays) |
| `CertificateExpectedCARootName` | Root CA Subject | `CN=Contoso Root CA, O=Contoso Corporation, C=US` |
| `CertificateExpectedCARootThumbprint` | Root CA SHA-1 | `A1B2C3D4E5F6G7H8I9J0K1L2M3N4O5P6Q7R8S9T0` |
| `CertificateExpectedSubordinateCAsJson` | Minified JSON | `[{"name":"CN=Contoso Device Management CA, O=Contoso, C=US","thumbprint":"789ABC..."}]` |
| `CertificateThumbprints` | Client cert allowlist (optional) | `ABC123...,DEF456...,GHI789...` |

3. Click **Save** and **Continue** (Function App will restart)

**Azure CLI:**
```bash
# Set mutual TLS settings
az functionapp config appsettings set \
  --name <function-app-name> \
  --resource-group <resource-group> \
  --settings \
    RequireCertificateAuthentication=true \
    CertificateValidateChain=true \
    CertificateValidateExpiration=true \
    CertificateCheckRevocation=false \
    CertificateExpectedCARootName="CN=Contoso Root CA, O=Contoso Corporation, C=US" \
    CertificateExpectedCARootThumbprint="A1B2C3D4E5F6G7H8I9J0K1L2M3N4O5P6Q7R8S9T0" \
    'CertificateExpectedSubordinateCAsJson=[{"name":"CN=Contoso Device Management CA, O=Contoso, C=US","thumbprint":"789ABC012DEF345GHI678JKL901MNO234PQR567"}]'
```

---

### Step 4: Configure Clients

#### 4.1 Update Client Configuration

**appsettings.json** (SecureBootWatcher.Client):
```json
{
  "SecureBootWatcher": {
    "Sinks": {
      "EnableAzureFunction": true,
      "EnableAzureQueue": false,
      "ExecutionStrategy": "StopOnFirstSuccess",
      "SinkPriority": "AzureFunction",
      "AzureFunction": {
        "FunctionUrl": "https://your-function-app.azurewebsites.net/api/reports",
        "ApiKey": "your-api-key-here",
        "HttpTimeout": "00:00:30",
        "UseApiKeyAsQueryParameter": false,
        "UseCertificateAuth": true,
        "CertificateThumbprint": "ABC123DEF456789012345678901234567890ABCD",
        "CertificateStoreLocation": "LocalMachine",
        "CertificateStoreName": "My"
      }
    }
  }
}
```

**Key fields:**
- `UseCertificateAuth`: `true` (enable client certificate)
- `CertificateThumbprint`: Client certificate thumbprint (from Step 1.4)
- `CertificateStoreLocation`: `LocalMachine` (for device certs) or `CurrentUser`
- `CertificateStoreName`: `My` (Personal certificate store)

#### 4.2 Install Client Certificate on Device

**Option A: Group Policy (Domain-joined devices)**
```
Computer Configuration
  └── Policies
      └── Windows Settings
          └── Security Settings
              └── Public Key Policies
                  └── Automatic Certificate Request Settings
```

**Option B: Intune (MDM-enrolled devices)**
1. Create **SCEP** or **PKCS** certificate profile
2. Deploy to device groups
3. Certificate auto-enrolled to `LocalMachine\My`

**Option C: Manual Installation (Testing)**
```powershell
# Import PFX certificate
$certPath = "C:\Temp\device-cert.pfx"
$certPassword = ConvertTo-SecureString -String "YourPassword" -Force -AsPlainText
Import-PfxCertificate -FilePath $certPath -CertStoreLocation Cert:\LocalMachine\My -Password $certPassword
```

---

## Testing & Validation

### Test 1: Verify Certificate Chain Locally

**PowerShell:**
```powershell
# Load client certificate
$clientCert = Get-ChildItem -Path Cert:\LocalMachine\My | 
    Where-Object { $_.Thumbprint -eq "ABC123DEF456..." }

# Build chain
$chain = New-Object System.Security.Cryptography.X509Certificates.X509Chain
$chain.ChainPolicy.RevocationMode = "NoCheck"
$buildResult = $chain.Build($clientCert)

if ($buildResult) {
    Write-Host "✅ Certificate chain is valid" -ForegroundColor Green
    
    # Display chain
    foreach ($element in $chain.ChainElements) {
        Write-Host "  - $($element.Certificate.Subject)" -ForegroundColor Cyan
        Write-Host "    Thumbprint: $($element.Certificate.Thumbprint)" -ForegroundColor Gray
        Write-Host "    Issuer: $($element.Certificate.Issuer)" -ForegroundColor Gray
        Write-Host ""
    }
} else {
    Write-Host "❌ Certificate chain validation failed" -ForegroundColor Red
    $chain.ChainStatus | ForEach-Object {
        Write-Host "  - $($_.StatusInformation)" -ForegroundColor Yellow
    }
}
```

**Expected Output:**
```
✅ Certificate chain is valid
  - CN=DESKTOP-12345.contoso.com
    Thumbprint: ABC123DEF456789012345678901234567890ABCD
    Issuer: CN=Contoso Device Management CA, O=Contoso, C=US

  - CN=Contoso Device Management CA, O=Contoso, C=US
    Thumbprint: 789ABC012DEF345GHI678JKL901MNO234PQR567
    Issuer: CN=Contoso Root CA, O=Contoso Corporation, C=US

  - CN=Contoso Root CA, O=Contoso Corporation, C=US
    Thumbprint: A1B2C3D4E5F6G7H8I9J0K1L2M3N4O5P6Q7R8S9T0
    Issuer: CN=Contoso Root CA, O=Contoso Corporation, C=US
```

### Test 2: Test Function Endpoint with Certificate

**PowerShell (Invoke-WebRequest):**
```powershell
# Load client certificate
$cert = Get-ChildItem -Path Cert:\LocalMachine\My | 
    Where-Object { $_.Thumbprint -eq "ABC123DEF456..." }

# Create test report JSON
$testReport = @{
    Device = @{
        MachineName = "TEST-DEVICE"
        Domain = "CONTOSO"
    }
    ClientVersion = "1.13.0"
    CollectedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json -Depth 10

# Send request with certificate
$response = Invoke-WebRequest `
    -Uri "https://your-function-app.azurewebsites.net/api/reports" `
    -Method POST `
    -Certificate $cert `
    -Headers @{ "X-API-Key" = "your-api-key" } `
    -Body $testReport `
    -ContentType "application/json"

Write-Host "Status: $($response.StatusCode)" -ForegroundColor Green
Write-Host "Response: $($response.Content)"
```

**Expected Response:**
```
Status: 202
Response: {"status":"accepted","message":"Report queued for processing","correlationId":"abc-123-def"}
```

### Test 3: Monitor Function Logs

**Azure Portal - Live Metrics:**
1. Navigate to Function App → **Application Insights** → **Live Metrics**
2. Send test request
3. Watch for log messages:
   - ✅ `"API key authentication successful"`
   - ✅ `"Client certificate present. Subject=..."`
   - ✅ `"Root CA name validated: CN=Contoso Root CA..."`
   - ✅ `"Subordinate CA found in chain: CN=Contoso Device Management CA..."`
   - ✅ `"All expected Subordinate CAs validated successfully"`
   - ✅ `"Report forwarded to Azure Queue successfully"`

**Azure CLI (Stream logs):**
```bash
az webapp log tail \
  --name <function-app-name> \
  --resource-group <resource-group>
```

---

## Troubleshooting

### Error: "Invalid or missing client certificate"

**Symptom:**
```
HTTP 403 Forbidden
"Invalid or missing client certificate"
```

**Possible Causes & Solutions:**

#### 1. Certificate not sent by client
- **Check:** Verify `UseCertificateAuth = true` in client config
- **Check:** Confirm certificate exists in specified store location
- **Fix:** Install client certificate in correct store (`LocalMachine\My`)

#### 2. Mutual TLS not enabled in App Service
- **Check:** Function App → Configuration → General settings → Client certificate mode
- **Fix:** Set to "Optional" or "Required"

#### 3. Certificate expired
- **Check:** `NotBefore` and `NotAfter` dates
- **Fix:** Renew certificate and deploy to clients

```powershell
$cert = Get-ChildItem Cert:\LocalMachine\My\ABC123...
Write-Host "Valid from: $($cert.NotBefore)"
Write-Host "Valid until: $($cert.NotAfter)"
Write-Host "Expired: $($cert.NotAfter -lt (Get-Date))"
```

---

### Error: "Root CA name mismatch"

**Symptom (Function logs):**
```
Root CA name mismatch. Expected=CN=Contoso Root CA, O=Contoso, C=US, Actual=CN=Other Root CA
```

**Possible Causes:**
1. Client certificate issued by different PKI
2. Typo in `CertificateExpectedCARootName` configuration
3. Certificate chain incomplete (missing intermediate CAs)

**Solution:**
```powershell
# Verify Root CA in client certificate chain
$cert = Get-ChildItem Cert:\LocalMachine\My\ABC123...
$chain = New-Object System.Security.Cryptography.X509Certificates.X509Chain
$chain.Build($cert) | Out-Null
$rootCert = $chain.ChainElements[$chain.ChainElements.Count - 1].Certificate

Write-Host "Root CA Subject: $($rootCert.Subject)"
# Compare with Function config: CertificateExpectedCARootName

# Update Function App setting if needed
az functionapp config appsettings set \
  --name <function-app-name> \
  --resource-group <resource-group> \
  --settings CertificateExpectedCARootName="$($rootCert.Subject)"
```

---

### Error: "Expected Subordinate CA not found in chain"

**Symptom (Function logs):**
```
Expected Subordinate CA not found in chain: Name=CN=Contoso Device Management CA, Thumbprint=789ABC...
```

**Possible Causes:**
1. Client certificate issued by different Issuing CA (e.g., "User CA" instead of "Device CA")
2. Subordinate CA certificate not installed on client machine
3. Typo in Subordinate CA name or thumbprint

**Solution:**

#### Step 1: Verify actual Issuing CA
```powershell
# Check who issued the client certificate
$cert = Get-ChildItem Cert:\LocalMachine\My\ABC123...
Write-Host "Issued by: $($cert.Issuer)"

# Build full chain and list all CAs
$chain = New-Object System.Security.Cryptography.X509Certificates.X509Chain
$chain.Build($cert) | Out-Null

Write-Host "`nCertificate Chain:"
for ($i = 0; $i -lt $chain.ChainElements.Count; $i++) {
    $element = $chain.ChainElements[$i]
    Write-Host "$i. Subject: $($element.Certificate.Subject)"
    Write-Host "   Thumbprint: $($element.Certificate.Thumbprint)"
}
```

#### Step 2: Compare with Function configuration
```bash
# Get current config
az functionapp config appsettings list \
  --name <function-app-name> \
  --resource-group <resource-group> \
  --query "[?name=='CertificateExpectedSubordinateCAsJson'].value" -o tsv
```

#### Step 3: Update configuration if needed
```bash
# Update to match actual Issuing CA
az functionapp config appsettings set \
  --name <function-app-name> \
  --resource-group <resource-group> \
  --settings 'CertificateExpectedSubordinateCAsJson=[{"name":"CN=Actual Issuing CA Subject","thumbprint":"ActualThumbprint"}]'
```

---

### Error: "Certificate chain validation failed"

**Symptom (Function logs):**
```
Certificate chain validation failed. ChainStatus=UntrustedRoot, PartialChain
```

**Possible Causes:**
1. Root CA not trusted on Function App (Azure App Service)
2. Intermediate CA certificates missing
3. Certificate chain incomplete

**Solution:**

#### For Azure App Service:
Azure App Service uses the **Windows Trusted Root Store**. If your enterprise Root CA is not in the public trust store, you need to bundle intermediate certificates with the client certificate.

**Export full chain from client:**
```powershell
# Export certificate with full chain (PFX)
$cert = Get-ChildItem Cert:\LocalMachine\My\ABC123...
$password = ConvertTo-SecureString -String "TempPassword" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "C:\Temp\client-with-chain.pfx" -Password $password -ChainOption BuildChain
```

**Alternatively, disable chain validation** (⚠️ less secure):
```bash
az functionapp config appsettings set \
  --name <function-app-name> \
  --resource-group <resource-group> \
  --settings CertificateValidateChain=false
```

**Note:** If `ValidateChain=false`, you **must** configure `ExpectedCARootThumbprint` and `ExpectedSubordinateCAsJson` to maintain security!

---

## Best Practices

### 1. Defense-in-Depth: Use All Validation Layers

**Recommended Configuration:**
```json
{
  "RequireCertificateAuthentication": true,
  "CertificateThumbprints": "ABC123...,DEF456...",  // Client allowlist
  "CertificateValidateChain": true,
  "CertificateExpectedCARootName": "CN=Contoso Root CA, ...",
  "CertificateExpectedCARootThumbprint": "A1B2C3D4...",
  "CertificateExpectedSubordinateCAsJson": "[{...}]"
}
```

**Security Levels:**
- 🔴 **Low**: Only API key
- 🟡 **Medium**: API key + certificate (any valid cert)
- 🟢 **High**: API key + certificate + Root CA validation
- 🟢 **Very High**: API key + cert + Root CA + Subordinate CA validation
- 🟢 **Maximum**: Above + client thumbprint allowlist

---

### 2. Certificate Lifecycle Management

#### Monitor Certificate Expiration
```powershell
# Check client certificates expiring in next 30 days
Get-ChildItem -Path Cert:\LocalMachine\My | 
    Where-Object { $_.NotAfter -lt (Get-Date).AddDays(30) -and $_.HasPrivateKey } |
    Select-Object Subject, Thumbprint, NotAfter |
    Format-Table -AutoSize
```

#### Automated Renewal
- Use **SCEP** (Simple Certificate Enrollment Protocol) for auto-renewal
- Configure auto-enrollment via Group Policy or Intune
- Monitor Azure Monitor alerts for expiring certificates

---

### 3. Separate Issuing CAs by Purpose

**Recommended PKI Structure:**
```
Root CA
├── Device Management CA → Devices managed by IT
├── IoT CA → IoT sensors and embedded devices
├── User CA → User authentication certificates
└── Server CA → Web servers and internal services
```

**Segregation Benefits:**
- Different validation periods (devices: 5 years, users: 2 years)
- Different revocation policies
- Granular access control per device type
- Easier compliance auditing

---

### 4. CRL Configuration

**When to enable `CertificateCheckRevocation=true`:**
- ✅ High-security environments
- ✅ Compliance requirements (PCI-DSS, HIPAA)
- ✅ CRL server is reliable and fast

**When to disable:**
- ❌ CRL server unreachable (causes timeouts)
- ❌ Offline environments
- ❌ Performance-critical scenarios

**Alternative:** Use **OCSP Stapling** (handled by Azure App Service automatically)

---

### 5. Logging and Monitoring

**Configure Application Insights:**
```bash
az functionapp config appsettings set \
  --name <function-app-name> \
  --resource-group <resource-group> \
  --settings APPINSIGHTS_INSTRUMENTATIONKEY="your-key"
```

**Monitor Key Metrics:**
- Certificate validation failures (403 responses)
- Certificate expiration warnings
- Root CA/Subordinate CA mismatch errors
- Revocation check timeouts

**Create Alert Rules:**
```bash
# Alert on certificate validation failures
az monitor metrics alert create \
  --name "Certificate Validation Failures" \
  --resource-group <resource-group> \
  --scopes /subscriptions/.../functionApps/your-function \
  --condition "count requests where resultCode == 403 > 10" \
  --window-size 5m \
  --evaluation-frequency 1m
```

---

## Security Considerations

### 1. Private Key Protection

**Client Certificates:**
- ✅ Store in **Trusted Platform Module (TPM)** if available
- ✅ Use **non-exportable** private keys
- ✅ Mark private keys as **machine-only** (not user-accessible)
- ❌ Never store certificates in user profiles for device auth

**PowerShell - Request non-exportable certificate:**
```powershell
$certRequest = New-Object -ComObject X509Enrollment.CX509CertificateRequestPkcs10
$certRequest.InitializeFromPrivateKey(1, $privateKey, "")
$certRequest.PrivateKeyExportable = $false
```

---

### 2. Revocation Strategy

**Immediate Revocation:**
1. Revoke certificate in CA
2. Publish updated CRL
3. Wait for CRL propagation (can take hours)
4. Alternative: Remove thumbprint from `CertificateThumbprints` allowlist (immediate)

**Best Practice:** Use **both** CRL and allowlist for faster revocation

---

### 3. Audit Logging

**Enable Diagnostic Settings:**
```bash
az monitor diagnostic-settings create \
  --name "CertAuthLogs" \
  --resource /subscriptions/.../functionApps/your-function \
  --logs '[{"category":"FunctionAppLogs","enabled":true}]' \
  --workspace /subscriptions/.../workspaces/your-log-analytics
```

**Query Certificate Auth Events:**
```kusto
// Log Analytics query
FunctionAppLogs
| where Message contains "Certificate authentication"
| project TimeGenerated, Message, Level
| order by TimeGenerated desc
```

---

### 4. Disaster Recovery

**Backup Critical Data:**
- Root CA certificate (export to secure storage)
- Subordinate CA certificates
- Function App configuration (export ARM template)
- Client certificate thumbprint inventory

**Recovery Procedure:**
1. Restore Function App from ARM template
2. Re-configure Application Settings
3. Re-enable mutual TLS
4. Validate with test certificate

---

## Appendix A: Quick Reference Commands

### Extract Certificate Information (PowerShell)

```powershell
# Load certificate by thumbprint
$cert = Get-ChildItem -Path Cert:\LocalMachine\My | Where-Object { $_.Thumbprint -eq "ABC123..." }

# Basic info
$cert.Subject
$cert.Issuer
$cert.Thumbprint
$cert.NotBefore
$cert.NotAfter

# Build and display chain
$chain = New-Object System.Security.Cryptography.X509Certificates.X509Chain
$chain.Build($cert) | Out-Null
$chain.ChainElements | ForEach-Object { $_.Certificate.Subject }

# Export certificate (no private key)
Export-Certificate -Cert $cert -FilePath "C:\Temp\cert.cer"

# Export with private key (PFX)
$pwd = ConvertTo-SecureString -String "Password123" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "C:\Temp\cert.pfx" -Password $pwd
```

### Extract Certificate Information (OpenSSL)

```bash
# Display certificate details
openssl x509 -in cert.pem -text -noout

# Extract Subject
openssl x509 -in cert.pem -noout -subject

# Extract Issuer
openssl x509 -in cert.pem -noout -issuer

# Extract Thumbprint (SHA-1)
openssl x509 -in cert.pem -noout -fingerprint -sha1

# Verify certificate chain
openssl verify -CAfile root.pem -untrusted intermediate.pem client.pem

# Display full chain
openssl s_client -connect your-function-app.azurewebsites.net:443 -showcerts
```

---

## Appendix B: Configuration Templates

### Template 1: Single Issuing CA (Recommended for Most Environments)

```json
{
  "ApiKey": "@Microsoft.KeyVault(SecretUri=...)",
  "QueueStorageUri": "https://prodstorageaccount.queue.core.windows.net",
  "QueueName": "secureboot-reports",
  
  "RequireCertificateAuthentication": "true",
  "CertificateValidateChain": "true",
  "CertificateValidateExpiration": "true",
  "CertificateCheckRevocation": "false",
  
  "CertificateExpectedCARootName": "CN=Contoso Root CA, O=Contoso Corporation, C=US",
  "CertificateExpectedCARootThumbprint": "A1B2C3D4E5F6G7H8I9J0K1L2M3N4O5P6Q7R8S9T0",
  
  "CertificateExpectedSubordinateCAsJson": "[{\"name\":\"CN=Contoso Device Management CA, O=Contoso, C=US\",\"thumbprint\":\"789ABC012DEF345GHI678JKL901MNO234PQR567\"}]"
}
```

---

### Template 2: Multiple Issuing CAs (Multi-Region or Failover)

```json
{
  "ApiKey": "@Microsoft.KeyVault(SecretUri=...)",
  "QueueStorageUri": "https://prodstorageaccount.queue.core.windows.net",
  "QueueName": "secureboot-reports",
  
  "RequireCertificateAuthentication": "true",
  "CertificateValidateChain": "true",
  "CertificateValidateExpiration": "true",
  "CertificateCheckRevocation": "false",
  
  "CertificateExpectedCARootName": "CN=Contoso Root CA, O=Contoso Corporation, C=US",
  "CertificateExpectedCARootThumbprint": "A1B2C3D4E5F6G7H8I9J0K1L2M3N4O5P6Q7R8S9T0",
  
  "CertificateExpectedSubordinateCAsJson": "[{\"name\":\"CN=Contoso Device CA US-East\",\"thumbprint\":\"789ABC...\"},{\"name\":\"CN=Contoso Device CA EU-West\",\"thumbprint\":\"890DEF...\"},{\"name\":\"CN=Contoso Device CA AP-South\",\"thumbprint\":\"901GHI...\"}]"
}
```

---

### Template 3: Maximum Security (With Client Allowlist)

```json
{
  "ApiKey": "@Microsoft.KeyVault(SecretUri=...)",
  "QueueStorageUri": "https://prodstorageaccount.queue.core.windows.net",
  "QueueName": "secureboot-reports",
  
  "RequireCertificateAuthentication": "true",
  "CertificateValidateChain": "true",
  "CertificateValidateExpiration": "true",
  "CertificateCheckRevocation": "true",
  
  "CertificateExpectedCARootName": "CN=Contoso Root CA, O=Contoso Corporation, C=US",
  "CertificateExpectedCARootThumbprint": "A1B2C3D4E5F6G7H8I9J0K1L2M3N4O5P6Q7R8S9T0",
  
  "CertificateExpectedSubordinateCAsJson": "[{\"name\":\"CN=Contoso Device Management CA, O=Contoso, C=US\",\"thumbprint\":\"789ABC012DEF345GHI678JKL901MNO234PQR567\"}]",
  
  "CertificateThumbprints": "ABC123DEF456789012345678901234567890ABCD,DEF456GHI789012345678901234567890ABCDE,GHI789JKL012345678901234567890ABCDEF01"
}
```

---

## Appendix C: Related Documentation

- [Azure App Service Mutual TLS](https://learn.microsoft.com/en-us/azure/app-service/app-service-web-configure-tls-mutual-auth)
- [X.509 Certificate Validation](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509chain)
- [Azure Function Authentication](https://learn.microsoft.com/en-us/azure/azure-functions/security-concepts)
- [PKI Best Practices](https://learn.microsoft.com/en-us/windows-server/identity/ad-cs/certification-authority-guidance)
- [Secure Boot Certificate Watcher - Main Documentation](../README.md)

---

## Support

For issues or questions:
1. Check [Troubleshooting](#troubleshooting) section
2. Review Function App logs in Application Insights
3. Open an issue on GitHub: https://github.com/robgrame/Nimbus.BootCertWatcher/issues

---

**Document Version:** 1.0  
**Last Updated:** 2025-01-XX  
**Author:** Secure Boot Certificate Watcher Team
