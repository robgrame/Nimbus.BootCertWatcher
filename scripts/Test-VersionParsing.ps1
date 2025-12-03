# Test-VersionParsing.ps1
# Test della logica di parsing della versione come nel _Layout.cshtml

Write-Host "Testing Version Parsing Logic" -ForegroundColor Cyan
Write-Host "=" * 60

# Test cases
$testCases = @(
    @{ Input = "1.11.0"; Expected = "1.11.0"; Description = "Simple version" },
    @{ Input = "1.11.0+abc123"; Expected = "1.11.0"; Description = "Version with commit hash" },
    @{ Input = "1.11.0-alpha.0"; Expected = "1.11.0"; Description = "Pre-release version" },
    @{ Input = "1.11.0-alpha.0+abc123"; Expected = "1.11.0"; Description = "Pre-release with commit hash" },
    @{ Input = "1.12.0-beta.1+def456"; Expected = "1.12.0"; Description = "Beta with commit hash" },
    @{ Input = "2.0.0-rc.3"; Expected = "2.0.0"; Description = "Release candidate" },
    @{ Input = $null; Expected = "1.0.0"; Description = "Null version (fallback)" },
    @{ Input = ""; Expected = "1.0.0"; Description = "Empty version (fallback)" },
    @{ Input = "   "; Expected = "1.0.0"; Description = "Whitespace version (fallback)" }
)

$passCount = 0
$failCount = 0

foreach ($testCase in $testCases) {
    $fullVersion = $testCase.Input
    
    # Same logic as _Layout.cshtml
    if ([string]::IsNullOrWhiteSpace($fullVersion)) {
        $fullVersion = "1.0.0"
    }
    
    $shortVersion = $fullVersion
    
    # Remove commit hash (everything after '+')
    if ($shortVersion.Contains('+')) {
        $shortVersion = $shortVersion.Substring(0, $shortVersion.IndexOf('+'))
    }
    
    # Remove pre-release suffix (everything after '-')
    if ($shortVersion.Contains('-')) {
        $shortVersion = $shortVersion.Substring(0, $shortVersion.IndexOf('-'))
    }
    
    # Ensure not empty
    if ([string]::IsNullOrWhiteSpace($shortVersion)) {
        $shortVersion = "1.0.0"
    }
    
    # Check result
    $passed = $shortVersion -eq $testCase.Expected
    
    if ($passed) {
        $passCount++
        Write-Host "? PASS: " -NoNewline -ForegroundColor Green
    } else {
        $failCount++
        Write-Host "? FAIL: " -NoNewline -ForegroundColor Red
    }
    
    Write-Host "$($testCase.Description)" -ForegroundColor White
    Write-Host "    Input:    '$($testCase.Input)'" -ForegroundColor Gray
    Write-Host "    Expected: '$($testCase.Expected)'" -ForegroundColor Gray
    Write-Host "    Got:      '$shortVersion'" -ForegroundColor $(if ($passed) { 'Green' } else { 'Red' })
    Write-Host ""
}

# Summary
Write-Host "Test Results Summary" -ForegroundColor Cyan
Write-Host "=" * 60
Write-Host "Total Tests:  $($testCases.Count)" -ForegroundColor White
Write-Host "Passed:       $passCount" -ForegroundColor Green
Write-Host "Failed:       $failCount" -ForegroundColor $(if ($failCount -gt 0) { 'Red' } else { 'Green' })

if ($failCount -eq 0) {
    Write-Host "`n? All tests passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n? Some tests failed!" -ForegroundColor Red
    exit 1
}
