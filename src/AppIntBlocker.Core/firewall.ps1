# Firewall operations for AppIntBlockerGUI.
#
# The app runs this text with Windows PowerShell (-EncodedCommand), as administrator. The request
# arrives on standard input and the result leaves on standard output, each as base64-encoded UTF-8
# JSON: no temporary files an unelevated program could swap, no user text on a command line, and
# no dependence on the console code page.
#
# It uses the same cmdlets and rule names as AppIntBlocker.ps1, so either tool can remove the
# other's rules.

$ErrorActionPreference = "Stop"
$RuleNamePrefix = "AppBlocker Rule -"

function Get-AppBlockerRule {
    @(Get-NetFirewallRule -DisplayName "$($RuleNamePrefix)*" -ErrorAction SilentlyContinue)
}

function Export-FirewallPolicy([string]$Path) {
    # netsh may write to stderr; under "Stop" Windows PowerShell would turn that into an exception
    # before the exit code could be read.
    $ErrorActionPreference = "Continue"
    $output = & netsh.exe advfirewall export $Path 2>&1
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $Path)) {
        throw "Could not back up the firewall rules: $(($output | Out-String).Trim())"
    }
}

function Remove-AppBlockerRule($Names, $Result) {
    $wanted = @{}
    foreach ($name in @($Names)) { $wanted[$name] = $true }
    foreach ($rule in Get-AppBlockerRule) {
        if (-not $wanted.ContainsKey($rule.DisplayName)) { continue }
        try {
            $rule | Remove-NetFirewallRule -ErrorAction Stop
            $Result.removed++
        }
        catch {
            [void]$Result.failed.Add(@{ name = $rule.DisplayName; error = $_.Exception.Message })
        }
    }
}

$result = @{ ok = $true; removed = 0; created = 0; skipped = 0; failed = (New-Object System.Collections.ArrayList) }
try {
    $requestText = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String((@($input) -join "").Trim()))
    $request = ConvertFrom-Json -InputObject $requestText

    switch ($request.operation) {
        "list" {
            $result.names = @(Get-AppBlockerRule | ForEach-Object { $_.DisplayName })
        }
        "backup" {
            Export-FirewallPolicy $request.backupPath
            $result.backupPath = $request.backupPath
        }
        "remove" {
            Remove-AppBlockerRule $request.names $result
        }
        "block" {
            # In order: back up, remove the rules being replaced, add the new ones. A failed backup
            # stops before anything changes.
            if ($request.backupPath) {
                Export-FirewallPolicy $request.backupPath
                $result.backupPath = $request.backupPath
            }
            if ($request.removeNames) {
                Remove-AppBlockerRule $request.removeNames $result
            }

            # Read the existing names once instead of asking the firewall before every rule.
            $existing = @{}
            foreach ($rule in Get-AppBlockerRule) { $existing[$rule.DisplayName] = $true }
            foreach ($rule in @($request.rules)) {
                if ($existing.ContainsKey($rule.name)) {
                    $result.skipped++
                    continue
                }
                try {
                    New-NetFirewallRule -DisplayName $rule.name -Direction $rule.direction -Program $rule.program `
                        -Action Block -Profile Any -Enabled True -ErrorAction Stop | Out-Null
                    $existing[$rule.name] = $true
                    $result.created++
                }
                catch {
                    [void]$result.failed.Add(@{ name = $rule.name; error = $_.Exception.Message })
                }
            }
        }
        default {
            throw "Unknown operation '$($request.operation)'."
        }
    }
}
catch {
    $result.ok = $false
    $result.error = $_.Exception.Message
}

$resultJson = ConvertTo-Json -InputObject $result -Depth 5 -Compress
"RESULT:" + [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($resultJson))
