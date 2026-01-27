$taskName = "SecureBoot Log Analytics Reporter"
$taskPath = "\ACIInformatica\Automation"
$principal = "SYSTEM"
$scriptName = "PS-ScheduleScript.ps1"
$scriptToExecute = "SecureBoot-LogAnalytics.ps1"
$ScriptsFolder = "ACIInformatica"
$scriptPath = $(Join-Path $env:HOMEDRIVE\temp $ScriptsFolder)

# Log Analytics parameters
$WorkspaceId = 'b1cb1714-f830-4f55-ba09-43008391b354'
$WorkspaceKey = 'b22e4yB38C0X/y2UpXo54cXpUjBWYvwi/VYR1oM3WqCPVek/mGZ82UV9s1B61lzal2dc36FIERVUMoUx/O9j/Q=='
$LogType = 'SecureBootInventory'


function Copy-Script {
    param (
        [Parameter(Mandatory = $true)]
        [string]$ScriptName,
        [Parameter(Mandatory = $true)]
        [string]$DestinationPath
    )

    # Check if the script already exists in the destination path
    if (Test-Path -Path (Join-Path $DestinationPath $ScriptName)) {
        Write-Log -Message "Script '$ScriptName' already exists in '$DestinationPath'. Skipping copy."
        return
    }

    # Copy the script to the destination path
    try {

        Write-Log -Message "Copying script '$ScriptName' to '$DestinationPath'..."

        # Check if the destination path exists, if not create it
        if (!(Test-Path -Path $DestinationPath)) {
            New-Item -Path $DestinationPath -ItemType Directory -Force
            Write-Log -Message "Created destination path '$DestinationPath'."
        }
        # Copy the script to the destination path
        Copy-Item -Path $ScriptName -Destination $DestinationPath -Force
        Write-Log -Message "Script '$ScriptName' copied to '$DestinationPath'."
    }
    catch {
        Write-Log -Message "Failed to copy script '$ScriptName' to '$DestinationPath' due to $($_.Exception.Message)" -logLevel ERROR
    }
}


function Write-Log {
    param (
        [Parameter(Mandatory = $true)]
        [string]$Message,
        #define loglevel as INFO, WARNING, ERROR, VERBOSE
        [ValidateSet("INFO","WARNING","ERROR","VERBOSE")]
        [Parameter(Mandatory = $false)]
        [ValidateNotNullOrEmpty()]
        [string]$logLevel = "INFO",
        [string]$logsPath = $(Join-Path $env:TEMP "$ScriptsFolder\Logs") # default log path
    )


    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "$timestamp - $logLevel - $Message"

 
    # Check if the script folder exists
    if (!(Test-Path $scriptPath)){
        New-Item -Path $scriptPath -ItemType Directory -Force -Confirm:$false
       
    }
    # Check if the logs folder exists
    if (!(Test-Path $logsPath)){
            New-Item -Path $logsPath -ItemType Directory -Force -Confirm:$false
        }
 
    # rotate log file if it is larger than 2MB and rename it with a date and time stamp in the file name and  keep only the last 5 log files and delete the rest

    # get all log files in the log folder sorted by date and time
    $logFiles = Get-ChildItem -Path $logsPath -Filter "$scriptName*.log" -File | Sort-Object -Property LastWriteTime -Descending
    # check if there are more than 5 log files
    if ($logFiles.Count -gt 5) {  
        # skip the first 5 files and delete the rest
        $logFiles | Select-Object -Skip 5 | Remove-Item -Force -Confirm:$false
    }

    $logFile = Join-Path $logsPath "$scriptName.log"
    if (Test-Path $logFile) {
        $logFileSize = (Get-Item $logFile).length
        if ($logFileSize -gt 2MB) {
            Write-Log VERBOSE -Message "Rotating log file"
            $logFileDate = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
            Rename-Item -Path $logFile -NewName "$scriptName-$logFileDate.log"
            Write-Log VERBOSE -Message "Log file rotated"
        }
    }

    # Write the log message to the log file in unicode format to support special characters
    Add-Content -Path (Join-Path $logsPath "$scriptName.log") -Value $logMessage -Encoding unicode
}

Function CreateScheduleTaskFolder{
 
    param(
        [Parameter(Mandatory = $true)]
        [string] $taskPath
    )
       
        # Create the scheduled task folder if it does not exist
        write-log -Message "Checking if scheduled task folder $taskPath exists"
        $taskFolder = Get-ScheduledTask -TaskPath $taskPath -ErrorAction SilentlyContinue
        if ($null -ne $taskFolder) {
            write-log -Message "Scheduled task folder $taskPath already exists"
            return
        }

        $scheduleObject = New-Object -ComObject schedule.service
        $scheduleObject.Connect()
        $rootFolder = $scheduleObject.GetFolder("\")
        write-log -Message "Creating scheduled task folder $taskPath"
        Try {
            write-log -Message "Trying to create scheduled task folder $taskPath"
            $rootFolder.CreateFolder($taskPath)
            write-log -Message "Scheduled task folder $taskPath created"
        }
        Catch {
            write-log -Message "Failed to create scheduled task folder $taskPath due to $($_.Exception.Message)" -logLevel ERROR
            }
}

# Function to create the scheduled task
function New-ScheduledTask {
    param (
        [Parameter(Mandatory = $true)]
        [string]$TaskName,
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [ValidateScript({Test-Path $_ -PathType 'Container'})]
        [string]$ScriptPath,
        [Parameter(Mandatory = $true)]
        [string]$TaskPath
    )

    # check if the scheduled task already exists
    $taskExists = Get-ScheduledTask -TaskName $taskName -TaskPath $taskPath -ErrorAction SilentlyContinue
    if ($null -ne $taskExists) {
        Write-Log -Message "Scheduled task '$taskName' already exists"
        Write-Log -Message "Deleting scheduled task '$taskName'"
        try {
            Unregister-ScheduledTask -TaskName $taskName -TaskPath $taskPath -Confirm:$false
            Write-Log -Message "Scheduled task '$taskName' deleted"
        }
        catch {
            Write-Log ERROR -Message "Failed to delete scheduled task '$taskName' due to $($_.Exception.Message)"
        }
    }




        # Copy the script to the destination path
        Copy-Script -ScriptName $scriptToExecute -DestinationPath $ScriptPath
        



        # Create a new trigger for the task to run
        Write-Log -Message "Creating scheduled task"

        # create a new trigger for the task running on Wednesday and Saturday
        $trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Wednesday, Saturday -At 9am -RandomDelay 00:05:00

        # Create the scheduled task folder if it does not exist
        CreateScheduleTaskFolder $TaskPath

        # Create a new action for the task to run SecureBoot-LogAnalytics.ps1 with parameters
        write-log VERBOSE -Message "Creating scheduled task action"
        $scriptArgs = "-ExecutionPolicy Bypass -NonInteractive -NoProfile -File `"$($scriptPath)\$($scriptToExecute)`" -WorkspaceId $WorkspaceId -WorkspaceKey $WorkspaceKey -LogType $LogType"
        $taskAction = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $scriptArgs
        write-log VERBOSE -Message "Scheduled task action created with Log Analytics parameters"

        # Create the scheduled task
        Write-Log -Message "Creating scheduled task"
        try {
            Register-ScheduledTask -TaskName $TaskName -Action $taskAction -Trigger $trigger -User $principal -Force -TaskPath $TaskPath -Description "Run SecureBoot Log Analytics Reporter - Collects Secure Boot certificates and device info" -RunLevel Highest
            Write-Log -Message "Scheduled task '$TaskName' created"
            Write-Log -Message "Scheduled task '$TaskName' set to run as $principal"
        }
        catch {
            Write-Log ERROR -Message "Failed to create scheduled task '$TaskName' due to $($_.Exception.Message)"
        }

        
}




# Create scheduled task to reboot the computer every Saturday at 2pm

write-log -Message "Trying to create scheduled task '$taskName' to report Secure Boot inventory to Log Analytics"
# Call the function to create the scheduled task
New-ScheduledTask -TaskName $taskName -ScriptPath $scriptPath -TaskPath $taskPath

Write-Log -Message "Scheduled task '$taskName' created successfully"
Write-Log -Message "Target Log Analytics Workspace: $WorkspaceId"
Write-Log -Message "Log Type: $LogType"