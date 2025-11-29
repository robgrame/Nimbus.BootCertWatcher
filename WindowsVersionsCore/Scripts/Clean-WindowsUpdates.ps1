param (
    [Parameter(Mandatory=$false)]
    [string]$inputFile = "..\..\..\Downloads\windows10-updates.json",
    
    [Parameter(Mandatory=$false)]
    [string]$outputFile = "..\..\..\Downloads\windows10-updates-cleaned.json"
)

Write-Host "Cleaning Windows update data:"
Write-Host "Input file: $inputFile"
Write-Host "Output file: $outputFile"

# Make sure the input file exists
if (-not (Test-Path $inputFile)) {
    Write-Error "Input file not found: $inputFile"
    exit 1
}

# Build and run the console application
dotnet build Console\WindowsVersionsCore.Console.csproj -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build the console application"
    exit 1
}

$exePath = "Console\bin\Release\net10.0\WindowsVersionsCore.Console.exe"

if (-not (Test-Path $exePath)) {
    Write-Error "Console application not found at: $exePath"
    exit 1
}

# Run the cleaner
& $exePath $inputFile $outputFile

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to clean the Windows update data"
    exit 1
}

Write-Host "Windows update data successfully cleaned and saved to: $outputFile"