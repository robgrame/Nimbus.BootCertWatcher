# Test script to validate Task Scheduler trigger creation
# This script tests the trigger creation logic without actually installing anything

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("Once", "Daily", "Hourly", "Custom")]
    [string]$ScheduleType = "Custom",
    
    [Parameter(Mandatory = $false)]
    [string]$TaskTime = "08:00AM",
    
    [Parameter(Mandatory = $false)]
    [ValidateRange(1, 24)]
    [int]$RepeatEveryHours = 2,
    
    [Parameter(Mandatory = $false)]
    [ValidateRange(0, 1440)]
    [int]$RandomDelayMinutes = 10
)

Write-Host "Testing Task Scheduler trigger creation..." -ForegroundColor Cyan
Write-Host "Schedule Type: $ScheduleType" -ForegroundColor Yellow
Write-Host "Task Time: $TaskTime" -ForegroundColor Yellow
Write-Host "Repeat Every: $RepeatEveryHours hours" -ForegroundColor Yellow
Write-Host "Random Delay: 0-$RandomDelayMinutes minutes" -ForegroundColor Yellow
Write-Host ""

try {
    # Parse task time
    $taskDateTime = [DateTime]::Parse($TaskTime)
    Write-Host "? Parsed task time: $taskDateTime" -ForegroundColor Green

    # Create random delay TimeSpan (this is the MAXIMUM delay)
    $randomDelayTimeSpan = New-TimeSpan -Minutes $RandomDelayMinutes
    Write-Host "? Random delay TimeSpan: $randomDelayTimeSpan" -ForegroundColor Green

    # Task Scheduler maximum duration is 31 days
    $maxRepetitionDuration = New-TimeSpan -Days 31
    Write-Host "? Max repetition duration: $maxRepetitionDuration" -ForegroundColor Green

    # Create trigger based on schedule type
    $trigger = $null
    
    switch ($ScheduleType) {
        "Once" {
            $trigger = New-ScheduledTaskTrigger -Once -At $taskDateTime -RandomDelay $randomDelayTimeSpan
            Write-Host "? Created 'Once' trigger with RandomDelay" -ForegroundColor Green
        }
        "Daily" {
            $trigger = New-ScheduledTaskTrigger -Daily -At $taskDateTime -RandomDelay $randomDelayTimeSpan
            Write-Host "? Created 'Daily' trigger with RandomDelay" -ForegroundColor Green
        }
        "Hourly" {
            # RandomDelay is not supported with RepetitionInterval
            $trigger = New-ScheduledTaskTrigger -Once -At $taskDateTime -RepetitionInterval (New-TimeSpan -Hours 1) -RepetitionDuration $maxRepetitionDuration
            Write-Host "? Created 'Hourly' trigger (RandomDelay not supported with repetition)" -ForegroundColor Green
        }
        "Custom" {
            # RandomDelay is not supported with RepetitionInterval
            $trigger = New-ScheduledTaskTrigger -Once -At $taskDateTime -RepetitionInterval (New-TimeSpan -Hours $RepeatEveryHours) -RepetitionDuration $maxRepetitionDuration
            Write-Host "? Created 'Custom' trigger (every $RepeatEveryHours hours, RandomDelay not supported)" -ForegroundColor Green
        }
    }

    Write-Host ""
    Write-Host "Trigger Details:" -ForegroundColor Cyan
    Write-Host "  Start Time: $($trigger.StartBoundary)" -ForegroundColor White
    Write-Host "  Random Delay: $($trigger.RandomDelay)" -ForegroundColor White
    
    if ($trigger.Repetition) {
        Write-Host "  Repetition Interval: $($trigger.Repetition.Interval)" -ForegroundColor White
        Write-Host "  Repetition Duration: $($trigger.Repetition.Duration)" -ForegroundColor White
    }
    
    Write-Host ""
    Write-Host "? Test completed successfully!" -ForegroundColor Green
    Write-Host "  The trigger was created without errors and should work with Register-ScheduledTask" -ForegroundColor Green
}
catch {
    Write-Host ""
    Write-Host "? Test failed: $_" -ForegroundColor Red
    Write-Host $_.Exception.GetType().FullName -ForegroundColor Red
    exit 1
}
