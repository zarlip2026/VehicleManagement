#Requires -Version 5.1
[CmdletBinding()]
param([switch]$CheckOnly, [switch]$SkipSqlServer)

$ErrorActionPreference = 'Stop'

function Install-Prerequisite {
    param([string]$PackageId)
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        throw 'Install or update Microsoft App Installer from Microsoft Store to obtain winget, then rerun this script.'
    }
    Write-Host "Installing $PackageId. Complete installer and Windows permission prompts."
    & winget install --id $PackageId --exact --source winget --interactive --accept-source-agreements
    if ($LASTEXITCODE -ne 0) {
        throw "Installation did not finish successfully (exit code $LASTEXITCODE). If a restart was requested, restart Windows and rerun this script."
    }
    $env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
        [Environment]::GetEnvironmentVariable('Path', 'User')
}

function Test-DotNetPrerequisite {
    param([string]$Kind)
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { return $false }
    if ($Kind -eq 'SDK') {
        $installed = @(& dotnet --list-sdks)
        return ($LASTEXITCODE -eq 0 -and [bool]($installed -match '^10\.'))
    }
    $installed = @(& dotnet --list-runtimes)
    return ($LASTEXITCODE -eq 0 -and
        [bool]($installed -match '^Microsoft.NETCore.App 8\.') -and
        [bool]($installed -match '^Microsoft.AspNetCore.App 8\.'))
}

$exitCode = 0
Push-Location $PSScriptRoot
try {
    # SDK 10 supports the .slnx solution; the app and EF tool target .NET 8.
    foreach ($requirement in @(
        @{ Kind = 'Runtime'; Package = 'Microsoft.DotNet.SDK.8'; Label = '.NET 8 and ASP.NET Core 8 runtimes' },
        @{ Kind = 'SDK'; Package = 'Microsoft.DotNet.SDK.10'; Label = '.NET 10 SDK for the .slnx solution' }
    )) {
        if (-not (Test-DotNetPrerequisite -Kind $requirement.Kind)) {
            if ($CheckOnly) { throw "Missing: $($requirement.Label)." }
            Install-Prerequisite -PackageId $requirement.Package
            if (-not (Test-DotNetPrerequisite -Kind $requirement.Kind)) {
                throw "Still missing: $($requirement.Label). Reopen this script after completing installation or restarting Windows."
            }
        }
        Write-Host "Available: $($requirement.Label)"
    }

    if (-not $SkipSqlServer) {
        $sqlService = Get-Service -Name 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue
        if (-not $sqlService) {
            if ($CheckOnly) { throw 'SQL Express instance SQLEXPRESS was not found.' }
            Write-Host 'In the SQL installer, install the Database Engine using the SQLEXPRESS instance.'
            Install-Prerequisite -PackageId 'Microsoft.SQLServer.2022.Express'
            $sqlService = Get-Service -Name 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue
            if (-not $sqlService) {
                throw 'SQLEXPRESS was not found after installation. Complete SQL setup, then rerun. Use -SkipSqlServer if you already have another SQL instance.'
            }
        }
        Write-Host "SQL Express installed. Service state: $($sqlService.Status)"
        if ($sqlService.Status -ne 'Running') {
            Write-Warning 'Start SQL Server (SQLEXPRESS) in Windows Services before creating the application database.'
        }
    }

    if ($CheckOnly) {
        Write-Host 'Machine prerequisite checks passed. EF tool restore was not run in check-only mode.'
    }
    else {
        Write-Host 'Restoring the project-local Entity Framework command-line tool...'
        & dotnet tool restore --tool-manifest '.config/dotnet-tools.json'
        if ($LASTEXITCODE -ne 0) { throw "EF tool restore failed (exit code $LASTEXITCODE)." }
        Write-Host 'Prerequisites prepared. Follow README.md to configure the database, build and run the application.' -ForegroundColor Green
    }
}
catch {
    Write-Host "Prerequisite setup failed: $($_.Exception.Message)" -ForegroundColor Red
    $exitCode = 1
}
finally { Pop-Location }
exit $exitCode

