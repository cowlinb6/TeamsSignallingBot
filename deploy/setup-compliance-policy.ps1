<#
.SYNOPSIS
    Registers the signaling bot as a Teams compliance recording application and
    assigns its policy to a single pilot user, so Teams auto-invites the bot to
    that user's calls/meetings.

.DESCRIPTION
    Run from a Windows machine with the Microsoft Teams PowerShell module.
    The pilot user is the only account affected.

.NOTES
    Requires Teams admin. Install the module:  Install-Module MicrosoftTeams
#>

<#+
.EXAMPLE
    .\setup-compliance-policy.ps1 -BotAppId e5dddf52-c008-4028-a350-ed8cd1792e83 -PilotUserUpn ben@dimensions.team
#>

param(
    [Parameter(Mandatory = $true)] [string] $BotAppId,      # = app registration / Azure Bot Client ID
    [Parameter(Mandatory = $true)] [string] $PilotUserUpn,  # e.g. tester@contoso.onmicrosoft.com
    [string] $ApplicationName = "Signaling PoC Recorder",
    [string] $PolicyName      = "SignalingPoCPolicy"
)

$ErrorActionPreference = "Stop"

Import-Module MicrosoftTeams
Connect-MicrosoftTeams

Write-Host "Ensuring compliance recording policy '$PolicyName' exists..."
$policyIdentity = "Tag:$PolicyName"
$policy = Get-CsTeamsComplianceRecordingPolicy -Identity $PolicyName -ErrorAction SilentlyContinue
if (-not $policy) {
    $policy = Get-CsTeamsComplianceRecordingPolicy -Identity $policyIdentity -ErrorAction SilentlyContinue
}

if (-not $policy) {
    $policy = New-CsTeamsComplianceRecordingPolicy `
        -Identity $policyIdentity `
        -Enabled  $true `
        -Description $ApplicationName
    $policyIdentity = $policy.Identity
} else {
    $policyIdentity = $policy.Identity
    Write-Host "Policy '$policyIdentity' already exists."
}

Write-Host "Resolving Teams application instance for Bot AppId $BotAppId ..."
$appInstance = Find-CsOnlineApplicationInstance -SearchQuery $BotAppId -ExactMatchOnly -ErrorAction SilentlyContinue
if (-not $appInstance) {
    $appInstance = Get-CsOnlineApplicationInstance -ErrorAction Stop | Where-Object {
        ($_.PSObject.Properties.Name -contains 'ApplicationId' -and $_.ApplicationId -eq $BotAppId) -or
        ($_.PSObject.Properties.Name -contains 'AppId' -and $_.AppId -eq $BotAppId)
    }
}

if ($appInstance -is [System.Array]) {
    $appInstance = $appInstance | Select-Object -First 1
}

if (-not $appInstance) {
    $domain = ($PilotUserUpn -split '@')[1]
    $baseName = ($PolicyName + '-recorder').ToLower() -replace '[^a-z0-9-]', ''
    $candidateUpn = "$baseName@$domain"
    $suffix = 0

    while (Get-CsOnlineApplicationInstance -Identity $candidateUpn -ErrorAction SilentlyContinue) {
        $suffix++
        $candidateUpn = "$baseName-$suffix@$domain"
    }

    Write-Host "Creating Teams application instance '$candidateUpn' for AppId $BotAppId ..."
    $appInstance = New-CsOnlineApplicationInstance `
        -UserPrincipalName $candidateUpn `
        -ApplicationId $BotAppId `
        -DisplayName $ApplicationName

    if (-not $appInstance) {
        throw "Failed to create a Teams application instance for application ID $BotAppId."
    }
}

$appInstanceId = if ($appInstance.PSObject.Properties.Name -contains 'ObjectId') {
    $appInstance.ObjectId
} elseif ($appInstance.PSObject.Properties.Name -contains 'Id') {
    $appInstance.Id
} elseif ($appInstance.PSObject.Properties.Name -contains 'Identity') {
    $appInstance.Identity
} else {
    throw "Unable to resolve the Teams application instance object id from the returned object."
}

$existingAssociation = Get-CsTeamsComplianceRecordingApplication -Filter "$policyIdentity*" -ErrorAction SilentlyContinue | Where-Object {
    $_.Identity -eq "$policyIdentity/$appInstanceId"
}

if ($existingAssociation) {
    Write-Host "Compliance recording association already exists for app instance $appInstanceId and policy $policyIdentity."
} else {
    Write-Host "Registering compliance recording application for app instance $appInstanceId ..."
    New-CsTeamsComplianceRecordingApplication `
        -Parent $policyIdentity `
        -Id $appInstanceId
}

Write-Host "Assigning policy to pilot user $PilotUserUpn ..."
Grant-CsTeamsComplianceRecordingPolicy `
    -Identity   $PilotUserUpn `
    -PolicyName $PolicyName

Write-Host "Done. Policy propagation can take a while. Place a test call as $PilotUserUpn."
Write-Host "To remove later: Grant-CsTeamsComplianceRecordingPolicy -Identity $PilotUserUpn -PolicyName `$null; Remove-CsTeamsComplianceRecordingPolicy -Identity $PolicyName"
