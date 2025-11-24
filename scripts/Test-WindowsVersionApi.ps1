# Test-WindowsVersionApi.ps1
# Tests the Windows Version API endpoints

[CmdletBinding()]
param(
    [Parameter()]
    [string]$ApiBaseUrl = "https://localhost:5001",
    
    [Parameter()]
    [switch]$SkipCertificateCheck,
    
    [Parameter()]
    [switch]$Verbose
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Windows Version API Test Suite" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "API Base URL: $ApiBaseUrl" -ForegroundColor White
Write-Host ""

# Skip certificate validation if requested (for local dev)
if ($SkipCertificateCheck) {
    Write-Host "??  Skipping SSL certificate validation" -ForegroundColor Yellow
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        # PowerShell Core
        $PSDefaultParameterValues['Invoke-RestMethod:SkipCertificateCheck'] = $true
        $PSDefaultParameterValues['Invoke-WebRequest:SkipCertificateCheck'] = $true
    } else {
        # Windows PowerShell
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
    }
}

$testResults = @{
    Passed = 0
    Failed = 0
    Skipped = 0
}

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Url,
        [string]$Method = "GET",
        [object]$Body = $null,
        [hashtable]$ExpectedFields = @{},
        [switch]$Optional
    )
    
    Write-Host ""
    Write-Host "?? Test: $Name" -ForegroundColor Cyan
    Write-Host "   URL: $Url" -ForegroundColor Gray
    Write-Host "   Method: $Method" -ForegroundColor Gray
    
    try {
        $params = @{
            Uri = $Url
            Method = $Method
            ContentType = "application/json"
            ErrorAction = "Stop"
        }
        
        if ($Body) {
            $params['Body'] = ($Body | ConvertTo-Json -Depth 10)
            Write-Host "   Body: $($params['Body'])" -ForegroundColor Gray
        }
        
        $response = Invoke-RestMethod @params
        
        Write-Host "   ? Request successful" -ForegroundColor Green
        
        # Validate expected fields
        $validationPassed = $true
        foreach ($field in $ExpectedFields.Keys) {
            $expectedType = $ExpectedFields[$field]
            
            if ($response.PSObject.Properties.Name -contains $field) {
                $actualValue = $response.$field
                $actualType = $actualValue.GetType().Name
                
                if ($Verbose) {
                    Write-Host "      Field '$field': $actualType = $actualValue" -ForegroundColor DarkGray
                }
                
                # Type validation (simplified)
                if ($expectedType -eq "array" -and $actualValue -isnot [array]) {
                    Write-Host "      ? Field '$field' should be array but is $actualType" -ForegroundColor Red
                    $validationPassed = $false
                }
            } else {
                Write-Host "      ? Missing expected field: $field" -ForegroundColor Red
                $validationPassed = $false
            }
        }
        
        if ($validationPassed) {
            Write-Host "   ? Response validation passed" -ForegroundColor Green
            $script:testResults.Passed++
            return $response
        } else {
            Write-Host "   ??  Response validation failed" -ForegroundColor Yellow
            $script:testResults.Failed++
            return $null
        }
    }
    catch {
        if ($Optional) {
            Write-Host "   ??  Test skipped (optional): $_" -ForegroundColor Yellow
            $script:testResults.Skipped++
        } else {
            Write-Host "   ? Request failed: $_" -ForegroundColor Red
            $script:testResults.Failed++
        }
        return $null
    }
}

# Test 1: Sync Windows Versions (POST)
Write-Host ""
Write-Host "???????????????????????????????????????" -ForegroundColor Yellow
Write-Host "Test Group 1: Synchronization" -ForegroundColor Yellow
Write-Host "???????????????????????????????????????" -ForegroundColor Yellow

$syncResult = Test-Endpoint `
    -Name "Sync Windows Versions" `
    -Url "$ApiBaseUrl/api/WindowsVersion/sync" `
    -Method "POST" `
    -ExpectedFields @{
        "success" = "bool"
        "versionsSynced" = "int"
        "buildsSynced" = "int"
    }

if ($syncResult -and $Verbose) {
    Write-Host ""
    Write-Host "   Sync Result:" -ForegroundColor Cyan
    Write-Host "   - Success: $($syncResult.success)" -ForegroundColor White
    Write-Host "   - Versions Synced: $($syncResult.versionsSynced)" -ForegroundColor White
    Write-Host "   - Builds Synced: $($syncResult.buildsSynced)" -ForegroundColor White
    if ($syncResult.errorMessage) {
        Write-Host "   - Error: $($syncResult.errorMessage)" -ForegroundColor Red
    }
}

# Test 2: Get All Versions (GET)
Write-Host ""
Write-Host "???????????????????????????????????????" -ForegroundColor Yellow
Write-Host "Test Group 2: Version Queries" -ForegroundColor Yellow
Write-Host "???????????????????????????????????????" -ForegroundColor Yellow

$versions = Test-Endpoint `
    -Name "Get All Windows Versions" `
    -Url "$ApiBaseUrl/api/WindowsVersion/versions" `
    -ExpectedFields @{}

if ($versions) {
    Write-Host "   Found $($versions.Count) Windows versions" -ForegroundColor Green
    
    if ($Verbose -and $versions.Count -gt 0) {
        Write-Host ""
        Write-Host "   Available Versions:" -ForegroundColor Cyan
        foreach ($version in $versions | Select-Object -First 5) {
            Write-Host "   - $($version.name) ($($version.version))" -ForegroundColor White
            Write-Host "     Released: $($version.releaseDate)" -ForegroundColor Gray
            Write-Host "     Last Synced: $($version.lastSyncedUtc)" -ForegroundColor Gray
        }
        
        if ($versions.Count -gt 5) {
            Write-Host "   ... and $($versions.Count - 5) more" -ForegroundColor Gray
        }
    }
}

# Test 3: Get Builds for a Version (GET)
if ($versions -and $versions.Count -gt 0) {
    $testVersion = $versions[0].version
    
    $builds = Test-Endpoint `
        -Name "Get Builds for Version '$testVersion'" `
        -Url "$ApiBaseUrl/api/WindowsVersion/versions/$testVersion/builds" `
        -ExpectedFields @{}
    
    if ($builds) {
        Write-Host "   Found $($builds.Count) builds for version $testVersion" -ForegroundColor Green
        
        if ($Verbose -and $builds.Count -gt 0) {
            Write-Host ""
            Write-Host "   Recent Builds:" -ForegroundColor Cyan
            foreach ($build in $builds | Select-Object -First 3) {
                $secureIcon = if ($build.isSecure) { "??" } else { "??" }
                $latestIcon = if ($build.isLatest) { "?" } else { "  " }
                
                Write-Host "   $secureIcon$latestIcon Build $($build.buildNumber)" -ForegroundColor White
                if ($build.releaseDate) {
                    Write-Host "        Released: $($build.releaseDate)" -ForegroundColor Gray
                }
                if ($build.kbArticle) {
                    Write-Host "        KB: $($build.kbArticle)" -ForegroundColor Gray
                }
                if ($build.securityNotes) {
                    Write-Host "        Notes: $($build.securityNotes)" -ForegroundColor Gray
                }
            }
        }
    }
}

# Test 4: Get Latest Secure Build (GET)
if ($versions -and $versions.Count -gt 0) {
    $testVersion = $versions[0].version
    
    $latestBuild = Test-Endpoint `
        -Name "Get Latest Secure Build for '$testVersion'" `
        -Url "$ApiBaseUrl/api/WindowsVersion/versions/$testVersion/latest-secure" `
        -ExpectedFields @{
            "buildNumber" = "string"
            "isSecure" = "bool"
            "isLatest" = "bool"
        }
    
    if ($latestBuild -and $Verbose) {
        Write-Host ""
        Write-Host "   Latest Secure Build: $($latestBuild.buildNumber)" -ForegroundColor Green
        Write-Host "   - Released: $($latestBuild.releaseDate)" -ForegroundColor White
        if ($latestBuild.kbArticle) {
            Write-Host "   - KB Article: $($latestBuild.kbArticle)" -ForegroundColor White
        }
    }
}

# Test 5: Check Build Security (GET)
Write-Host ""
Write-Host "???????????????????????????????????????" -ForegroundColor Yellow
Write-Host "Test Group 3: Security Checks" -ForegroundColor Yellow
Write-Host "???????????????????????????????????????" -ForegroundColor Yellow

# Test with known builds
$testBuilds = @("19045.3803", "22631.2861", "26100.1000")

foreach ($buildNumber in $testBuilds) {
    $buildStatus = Test-Endpoint `
        -Name "Check Build Security: $buildNumber" `
        -Url "$ApiBaseUrl/api/WindowsVersion/check-build/$buildNumber" `
        -ExpectedFields @{
            "buildNumber" = "string"
            "isSecure" = "bool"
            "isLatest" = "bool"
        } `
        -Optional
    
    if ($buildStatus) {
        $secureText = if ($buildStatus.isSecure) { "? SECURE" } else { "??  INSECURE" }
        $latestText = if ($buildStatus.isLatest) { "(Latest)" } else { "" }
        
        Write-Host "   Status: $secureText $latestText" -ForegroundColor $(if ($buildStatus.isSecure) { "Green" } else { "Yellow" })
        
        if ($buildStatus.securityNotes -and $Verbose) {
            Write-Host "   Notes: $($buildStatus.securityNotes)" -ForegroundColor Gray
        }
        
        if ($buildStatus.latestSecureBuild -and $Verbose) {
            Write-Host "   Latest Secure: $($buildStatus.latestSecureBuild)" -ForegroundColor Cyan
        }
    }
}

# Test 6: Get Build Statistics (GET)
Write-Host ""
Write-Host "???????????????????????????????????????" -ForegroundColor Yellow
Write-Host "Test Group 4: Statistics & Reporting" -ForegroundColor Yellow
Write-Host "???????????????????????????????????????" -ForegroundColor Yellow

$statistics = Test-Endpoint `
    -Name "Get Build Statistics" `
    -Url "$ApiBaseUrl/api/WindowsVersion/statistics" `
    -ExpectedFields @{
        "totalDevices" = "int"
        "devicesWithSecureBuilds" = "int"
        "devicesWithOutdatedBuilds" = "int"
        "secureBuildPercentage" = "double"
    } `
    -Optional

if ($statistics) {
    Write-Host ""
    Write-Host "   ?? Fleet Statistics:" -ForegroundColor Cyan
    Write-Host "   - Total Devices: $($statistics.totalDevices)" -ForegroundColor White
    Write-Host "   - Secure Builds: $($statistics.devicesWithSecureBuilds) ($($statistics.secureBuildPercentage.ToString('F1'))%)" -ForegroundColor Green
    Write-Host "   - Outdated Builds: $($statistics.devicesWithOutdatedBuilds)" -ForegroundColor Yellow
    Write-Host "   - Unknown Builds: $($statistics.devicesWithUnknownBuilds)" -ForegroundColor Gray
    
    if ($Verbose -and $statistics.buildDistribution -and $statistics.buildDistribution.Count -gt 0) {
        Write-Host ""
        Write-Host "   Build Distribution:" -ForegroundColor Cyan
        $statistics.buildDistribution.GetEnumerator() | 
            Sort-Object -Property Value -Descending | 
            Select-Object -First 10 | 
            ForEach-Object {
                Write-Host "   - Build $($_.Key): $($_.Value) devices" -ForegroundColor White
            }
    }
}

# Test 7: Get Devices with Outdated Builds (GET)
$outdatedDevices = Test-Endpoint `
    -Name "Get Devices with Outdated Builds" `
    -Url "$ApiBaseUrl/api/WindowsVersion/devices/outdated" `
    -ExpectedFields @{} `
    -Optional

if ($outdatedDevices) {
    Write-Host "   Found $($outdatedDevices.Count) devices with outdated builds" -ForegroundColor $(if ($outdatedDevices.Count -eq 0) { "Green" } else { "Yellow" })
    
    if ($Verbose -and $outdatedDevices.Count -gt 0) {
        Write-Host ""
        Write-Host "   Devices Needing Updates:" -ForegroundColor Cyan
        foreach ($device in $outdatedDevices | Select-Object -First 5) {
            Write-Host "   - $($device.machineName)" -ForegroundColor White
            Write-Host "     Current Build: $($device.osBuildNumber)" -ForegroundColor Gray
            if ($device.securityNotes) {
                Write-Host "     Notes: $($device.securityNotes)" -ForegroundColor Yellow
            }
        }
        
        if ($outdatedDevices.Count -gt 5) {
            Write-Host "   ... and $($outdatedDevices.Count - 5) more" -ForegroundColor Gray
        }
    }
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "? Passed:  $($testResults.Passed)" -ForegroundColor Green
Write-Host "? Failed:  $($testResults.Failed)" -ForegroundColor $(if ($testResults.Failed -eq 0) { "Green" } else { "Red" })
Write-Host "??  Skipped: $($testResults.Skipped)" -ForegroundColor Yellow
Write-Host ""

$totalTests = $testResults.Passed + $testResults.Failed + $testResults.Skipped
$successRate = if ($totalTests -gt 0) { ($testResults.Passed / $totalTests) * 100 } else { 0 }

Write-Host "Success Rate: $($successRate.ToString('F1'))%" -ForegroundColor $(if ($successRate -ge 80) { "Green" } elseif ($successRate -ge 50) { "Yellow" } else { "Red" })

if ($testResults.Failed -gt 0) {
    Write-Host ""
    Write-Host "??  Some tests failed. Check the API logs for details." -ForegroundColor Yellow
    exit 1
} else {
    Write-Host ""
    Write-Host "? All tests passed successfully!" -ForegroundColor Green
    exit 0
}
