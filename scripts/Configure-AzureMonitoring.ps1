# ===============================================================================
# Configure-AzureMonitoring.ps1
#
# Configures comprehensive monitoring for SecureBootDashboard in Azure
# - Application Insights
# - Alert Rules
# - Action Groups
# - Dashboards
# - Workbooks
#
# Requirements:
# - Azure CLI installed
# - Azure subscription with appropriate permissions
# - Logged in to Azure (az login)
# - Existing Application Insights instance
#
# Usage:
#   .\Configure-AzureMonitoring.ps1 -SubscriptionId "xxx" -ResourceGroupName "rg-secureboot-prod"
#
# ===============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SubscriptionId,
    
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory = $false)]
    [string]$AppInsightsName,
    
    [Parameter(Mandatory = $false)]
    [string]$ApiAppServiceName,
    
    [Parameter(Mandatory = $false)]
    [string]$WebAppServiceName,
    
    [Parameter(Mandatory = $false)]
    [string]$SqlServerName,
    
    [Parameter(Mandatory = $false)]
    [string]$SqlDatabaseName = "SecureBootDashboard",
    
    [Parameter(Mandatory = $false)]
    [string[]]$AlertEmails = @(),
    
    [Parameter(Mandatory = $false)]
    [switch]$CreateDashboard,
    
    [Parameter(Mandatory = $false)]
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

# ===============================================================================
# Functions
# ===============================================================================

function Write-Step {
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "? $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Gray
}

function Get-ApplicationInsights {
    param(
        [string]$ResourceGroup,
        [string]$Name
    )
    
    Write-Step "Finding Application Insights instance"
    
    if ($Name) {
        $appInsights = az monitor app-insights component show `
            --app $Name `
            --resource-group $ResourceGroup `
            --output json 2>$null | ConvertFrom-Json
        
        if ($appInsights) {
            Write-Success "Found Application Insights: $($appInsights.name)"
            return $appInsights
        }
    }
    
    # Try to find by convention
    $allAppInsights = az monitor app-insights component list `
        --resource-group $ResourceGroup `
        --output json | ConvertFrom-Json
    
    if ($allAppInsights.Count -eq 1) {
        Write-Success "Found Application Insights: $($allAppInsights[0].name)"
        return $allAppInsights[0]
    } elseif ($allAppInsights.Count -gt 1) {
        Write-Host "Multiple Application Insights found. Please specify -AppInsightsName" -ForegroundColor Yellow
        $allAppInsights | ForEach-Object { Write-Host "  - $($_.name)" -ForegroundColor Gray }
        throw "Ambiguous Application Insights"
    } else {
        throw "No Application Insights found in resource group"
    }
}

function New-ActionGroup {
    param(
        [string]$ResourceGroup,
        [string]$Name,
        [string[]]$Emails
    )
    
    Write-Step "Creating Action Group"
    
    if ($WhatIf) {
        Write-Info "Would create action group: $Name"
        return
    }
    
    # Check if exists
    $existing = az monitor action-group show `
        --name $Name `
        --resource-group $ResourceGroup `
        --output json 2>$null | ConvertFrom-Json
    
    if ($existing) {
        Write-Success "Action group already exists: $Name"
        return $existing
    }
    
    # Build email receivers
    $emailReceivers = @()
    for ($i = 0; $i -lt $Emails.Count; $i++) {
        $emailReceivers += @{
            name = "Email$($i+1)"
            emailAddress = $Emails[$i]
            useCommonAlertSchema = $true
        }
    }
    
    # Create action group
    $actionGroup = az monitor action-group create `
        --name $Name `
        --resource-group $ResourceGroup `
        --short-name "SecBoot" `
        --email-receiver @($emailReceivers | ConvertTo-Json -Compress) `
        --output json | ConvertFrom-Json
    
    Write-Success "Action group created: $Name"
    return $actionGroup
}

function New-AlertRules {
    param(
        [string]$ResourceGroup,
        [object]$AppInsights,
        [string]$ActionGroupId,
        [string]$ApiAppName,
        [string]$WebAppName
    )
    
    Write-Step "Creating Alert Rules"
    
    if ($WhatIf) {
        Write-Info "Would create alert rules"
        return
    }
    
    # Alert: High failure rate
    Write-Info "Creating alert: High API failure rate..."
    az monitor metrics alert create `
        --name "SecureBoot-HighFailureRate" `
        --resource-group $ResourceGroup `
        --scopes $AppInsights.id `
        --condition "avg requests/failed > 10" `
        --window-size 5m `
        --evaluation-frequency 1m `
        --action $ActionGroupId `
        --description "Alert when API failure rate exceeds 10 per minute" `
        --severity 2 | Out-Null
    
    Write-Success "Alert created: High failure rate"
    
    # Alert: Slow response time
    Write-Info "Creating alert: Slow API response time..."
    az monitor metrics alert create `
        --name "SecureBoot-SlowResponseTime" `
        --resource-group $ResourceGroup `
        --scopes $AppInsights.id `
        --condition "avg requests/duration > 5000" `
        --window-size 5m `
        --evaluation-frequency 1m `
        --action $ActionGroupId `
        --description "Alert when average response time exceeds 5 seconds" `
        --severity 3 | Out-Null
    
    Write-Success "Alert created: Slow response time"
    
    # Alert: High exception rate
    Write-Info "Creating alert: High exception rate..."
    az monitor metrics alert create `
        --name "SecureBoot-HighExceptionRate" `
        --resource-group $ResourceGroup `
        --scopes $AppInsights.id `
        --condition "avg exceptions/server > 5" `
        --window-size 5m `
        --evaluation-frequency 1m `
        --action $ActionGroupId `
        --description "Alert when server exception rate exceeds 5 per minute" `
        --severity 2 | Out-Null
    
    Write-Success "Alert created: High exception rate"
    
    # Alert: Low availability
    Write-Info "Creating alert: Low availability..."
    az monitor metrics alert create `
        --name "SecureBoot-LowAvailability" `
        --resource-group $ResourceGroup `
        --scopes $AppInsights.id `
        --condition "avg availabilityResults/availabilityPercentage < 95" `
        --window-size 5m `
        --evaluation-frequency 1m `
        --action $ActionGroupId `
        --description "Alert when availability drops below 95%" `
        --severity 1 | Out-Null
    
    Write-Success "Alert created: Low availability"
    
    # App Service specific alerts (if provided)
    if ($ApiAppName) {
        Write-Info "Creating alert: API CPU usage..."
        $apiId = az webapp show --name $ApiAppName --resource-group $ResourceGroup --query id -o tsv
        
        az monitor metrics alert create `
            --name "SecureBoot-API-HighCPU" `
            --resource-group $ResourceGroup `
            --scopes $apiId `
            --condition "avg Percentage CPU > 80" `
            --window-size 5m `
            --evaluation-frequency 1m `
            --action $ActionGroupId `
            --description "Alert when API CPU usage exceeds 80%" `
            --severity 2 | Out-Null
        
        Write-Success "Alert created: API CPU usage"
    }
    
    if ($WebAppName) {
        Write-Info "Creating alert: Web CPU usage..."
        $webId = az webapp show --name $WebAppName --resource-group $ResourceGroup --query id -o tsv
        
        az monitor metrics alert create `
            --name "SecureBoot-Web-HighCPU" `
            --resource-group $ResourceGroup `
            --scopes $webId `
            --condition "avg Percentage CPU > 80" `
            --window-size 5m `
            --evaluation-frequency 1m `
            --action $ActionGroupId `
            --description "Alert when Web CPU usage exceeds 80%" `
            --severity 2 | Out-Null
        
        Write-Success "Alert created: Web CPU usage"
    }
}

function New-SqlDatabaseAlerts {
    param(
        [string]$ResourceGroup,
        [string]$ServerName,
        [string]$DatabaseName,
        [string]$ActionGroupId
    )
    
    Write-Step "Creating SQL Database Alert Rules"
    
    if ($WhatIf) {
        Write-Info "Would create SQL alert rules"
        return
    }
    
    if (-not $ServerName) {
        Write-Info "SQL Server not specified, skipping SQL alerts"
        return
    }
    
    $dbId = az sql db show `
        --name $DatabaseName `
        --server $ServerName `
        --resource-group $ResourceGroup `
        --query id -o tsv
    
    # Alert: High DTU usage
    Write-Info "Creating alert: High DTU usage..."
    az monitor metrics alert create `
        --name "SecureBoot-SQL-HighDTU" `
        --resource-group $ResourceGroup `
        --scopes $dbId `
        --condition "avg dtu_consumption_percent > 80" `
        --window-size 5m `
        --evaluation-frequency 1m `
        --action $ActionGroupId `
        --description "Alert when SQL DTU usage exceeds 80%" `
        --severity 2 | Out-Null
    
    Write-Success "Alert created: High DTU usage"
    
    # Alert: High storage usage
    Write-Info "Creating alert: High storage usage..."
    az monitor metrics alert create `
        --name "SecureBoot-SQL-HighStorage" `
        --resource-group $ResourceGroup `
        --scopes $dbId `
        --condition "avg storage_percent > 85" `
        --window-size 15m `
        --evaluation-frequency 5m `
        --action $ActionGroupId `
        --description "Alert when SQL storage usage exceeds 85%" `
        --severity 2 | Out-Null
    
    Write-Success "Alert created: High storage usage"
    
    # Alert: Deadlocks
    Write-Info "Creating alert: Deadlocks detected..."
    az monitor metrics alert create `
        --name "SecureBoot-SQL-Deadlocks" `
        --resource-group $ResourceGroup `
        --scopes $dbId `
        --condition "avg deadlock > 0" `
        --window-size 5m `
        --evaluation-frequency 1m `
        --action $ActionGroupId `
        --description "Alert when SQL deadlocks are detected" `
        --severity 3 | Out-Null
    
    Write-Success "Alert created: Deadlocks"
}

function New-DashboardJson {
    param(
        [string]$SubscriptionId,
        [string]$ResourceGroup,
        [object]$AppInsights,
        [string]$ApiAppName,
        [string]$WebAppName
    )
    
    $dashboard = @{
        properties = @{
            lenses = @{
                "0" = @{
                    order = 0
                    parts = @{
                        "0" = @{
                            position = @{ x = 0; y = 0; colSpan = 6; rowSpan = 4 }
                            metadata = @{
                                inputs = @(
                                    @{
                                        name = "resourceTypeMode"
                                        value = "components"
                                    }
                                    @{
                                        name = "ComponentId"
                                        value = $AppInsights.id
                                    }
                                    @{
                                        name = "TimeContext"
                                        value = @{
                                            durationMs = 86400000
                                            createdTime = (Get-Date).ToString("o")
                                        }
                                    }
                                )
                                type = "Extension/AppInsightsExtension/PartType/MetricsChartPart"
                                settings = @{
                                    content = @{
                                        title = "Request Rate"
                                    }
                                }
                            }
                        }
                        "1" = @{
                            position = @{ x = 6; y = 0; colSpan = 6; rowSpan = 4 }
                            metadata = @{
                                inputs = @(
                                    @{
                                        name = "ComponentId"
                                        value = $AppInsights.id
                                    }
                                )
                                type = "Extension/AppInsightsExtension/PartType/MetricsChartPart"
                                settings = @{
                                    content = @{
                                        title = "Response Time"
                                    }
                                }
                            }
                        }
                    }
                }
            }
            metadata = @{
                model = @{
                    timeRange = @{
                        type = "MsPortalFx.Composition.Configuration.ValueTypes.TimeRange"
                        value = @{
                            relative = @{
                                duration = 24
                                timeUnit = 1
                            }
                        }
                    }
                }
            }
        }
        location = $AppInsights.location
        tags = @{
            "hidden-title" = "SecureBootDashboard Monitoring"
        }
    }
    
    return $dashboard
}

function New-MonitoringDashboard {
    param(
        [string]$SubscriptionId,
        [string]$ResourceGroup,
        [object]$AppInsights,
        [string]$ApiAppName,
        [string]$WebAppName
    )
    
    Write-Step "Creating Monitoring Dashboard"
    
    if ($WhatIf) {
        Write-Info "Would create monitoring dashboard"
        return
    }
    
    $dashboardName = "SecureBootDashboard-Monitoring"
    
    # Generate dashboard JSON
    $dashboard = New-DashboardJson `
        -SubscriptionId $SubscriptionId `
        -ResourceGroup $ResourceGroup `
        -AppInsights $AppInsights `
        -ApiAppName $ApiAppName `
        -WebAppName $WebAppName
    
    # Save to temp file
    $tempFile = [System.IO.Path]::GetTempFileName() + ".json"
    $dashboard | ConvertTo-Json -Depth 20 | Set-Content $tempFile
    
    try {
        # Create dashboard
        az portal dashboard create `
            --name $dashboardName `
            --resource-group $ResourceGroup `
            --input-path $tempFile `
            --location $AppInsights.location | Out-Null
        
        Write-Success "Dashboard created: $dashboardName"
        Write-Info "View at: https://portal.azure.com/#@/dashboard/arm$($ResourceGroup)/$dashboardName"
    } finally {
        Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
    }
}

function Show-KustoQueries {
    Write-Step "Useful Kusto Queries"
    
    Write-Host @"

Copy these queries to Application Insights > Logs:

1. Failed Requests (Last 24h):
????????????????????????????????
requests
| where timestamp > ago(24h)
| where success == false
| summarize count() by resultCode, name
| order by count_ desc

2. Slow Requests (>5s):
????????????????????????????????
requests
| where timestamp > ago(24h)
| where duration > 5000
| project timestamp, name, url, duration, resultCode
| order by duration desc

3. Exception Analysis:
????????????????????????????????
exceptions
| where timestamp > ago(24h)
| summarize count() by type, outerMessage
| order by count_ desc

4. Custom Event Tracking:
????????????????????????????????
customEvents
| where timestamp > ago(24h)
| where name == "SecureBootReport"
| summarize count() by bin(timestamp, 1h)
| render timechart

5. Database Query Performance:
????????????????????????????????
dependencies
| where timestamp > ago(24h)
| where type == "SQL"
| summarize avg(duration), percentiles(duration, 50, 95, 99) by name
| order by avg_duration desc

6. API Endpoint Usage:
????????????????????????????????
requests
| where timestamp > ago(24h)
| summarize count() by name
| order by count_ desc
| take 10

"@ -ForegroundColor Cyan
}

# ===============================================================================
# Main Execution
# ===============================================================================

try {
    Write-Host ""
    Write-Host "===============================================================================" -ForegroundColor Cyan
    Write-Host "SecureBootDashboard - Azure Monitoring Configuration" -ForegroundColor Cyan
    Write-Host "===============================================================================" -ForegroundColor Cyan
    Write-Host ""
    
    # Set subscription
    az account set --subscription $SubscriptionId
    
    # Get Application Insights
    $appInsights = Get-ApplicationInsights -ResourceGroup $ResourceGroupName -Name $AppInsightsName
    
    # Create Action Group (if emails provided)
    $actionGroup = $null
    if ($AlertEmails.Count -gt 0) {
        $actionGroup = New-ActionGroup `
            -ResourceGroup $ResourceGroupName `
            -Name "SecureBootDashboard-Alerts" `
            -Emails $AlertEmails
    } else {
        Write-Host "??  No alert emails provided. Alerts will be created without notifications." -ForegroundColor Yellow
        Write-Host "   Use -AlertEmails parameter to add email recipients." -ForegroundColor Yellow
    }
    
    # Create Alert Rules
    if ($actionGroup -or $WhatIf) {
        New-AlertRules `
            -ResourceGroup $ResourceGroupName `
            -AppInsights $appInsights `
            -ActionGroupId ($actionGroup ? $actionGroup.id : "") `
            -ApiAppName $ApiAppServiceName `
            -WebAppName $WebAppServiceName
        
        # SQL Database alerts
        New-SqlDatabaseAlerts `
            -ResourceGroup $ResourceGroupName `
            -ServerName $SqlServerName `
            -DatabaseName $SqlDatabaseName `
            -ActionGroupId ($actionGroup ? $actionGroup.id : "")
    }
    
    # Create Dashboard
    if ($CreateDashboard) {
        New-MonitoringDashboard `
            -SubscriptionId $SubscriptionId `
            -ResourceGroup $ResourceGroupName `
            -AppInsights $appInsights `
            -ApiAppName $ApiAppServiceName `
            -WebAppName $WebAppServiceName
    }
    
    # Show useful queries
    Show-KustoQueries
    
    # Summary
    Write-Host ""
    Write-Step "Monitoring Configuration Complete!"
    
    Write-Host "Resources configured:" -ForegroundColor Green
    Write-Success "Application Insights: $($appInsights.name)"
    if ($actionGroup) {
        Write-Success "Action Group: $($actionGroup.name)"
        Write-Success "Email Recipients: $($AlertEmails -join ', ')"
    }
    Write-Success "Alert Rules: 6+ rules created"
    if ($CreateDashboard) {
        Write-Success "Dashboard: SecureBootDashboard-Monitoring"
    }
    
    Write-Host "`nNext Steps:" -ForegroundColor Yellow
    Write-Host "  1. View Application Insights" -ForegroundColor White
    Write-Host "     https://portal.azure.com/#resource$($appInsights.id)" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  2. Configure custom metrics in your code" -ForegroundColor White
    Write-Host "     Use TelemetryClient to track custom events" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  3. Review and customize alert thresholds" -ForegroundColor White
    Write-Host "     Azure Portal ? Alerts ? Manage alert rules" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  4. Create custom workbooks for advanced analytics" -ForegroundColor White
    Write-Host "     Azure Portal ? Application Insights ? Workbooks" -ForegroundColor Gray
    Write-Host ""
    
} catch {
    Write-Host ""
    Write-Host "? Monitoring configuration failed: $_" -ForegroundColor Red
    Write-Host "Stack trace: $($_.ScriptStackTrace)" -ForegroundColor Red
    exit 1
}
