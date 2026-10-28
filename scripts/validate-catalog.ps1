#requires -Version 7.4
<#
.SYNOPSIS
    Validates catalog/webapps.json against its JSON Schema and the rules a schema can't express.

.DESCRIPTION
    Installed apps fetch the catalog from main once a day, so every merge is a production change;
    CI runs this script on every pull request. Checks (docs/WEBAPP-CATALOG.md §1–§2):
      - the file matches catalog/webapps.schema.json (types, required fields, formats, HTTPS);
      - app ids and names are unique;
      - the startUrl host and every popupAllow entry are covered by navAllow, matching whole DNS
        labels (google.com covers mail.google.com, never evilgoogle.com);
      - updated is a real date and not in the future.
    Exits with code 1 and one line per problem when anything fails.

.EXAMPLE
    pwsh ./scripts/validate-catalog.ps1
#>
[CmdletBinding()]
param(
    [string] $CatalogPath = (Join-Path $PSScriptRoot '../catalog/webapps.json'),
    [string] $SchemaPath = (Join-Path $PSScriptRoot '../catalog/webapps.schema.json')
)

$ErrorActionPreference = 'Stop'

function Test-DomainCover([string] $HostName, [string[]] $Domains) {
    foreach ($domain in $Domains) {
        if ($HostName -eq $domain -or $HostName.EndsWith(".$domain", [StringComparison]::Ordinal)) {
            return $true
        }
    }
    return $false
}

function Get-SchemaProblem([string] $Json, [string] $SchemaPath) {
    if (Test-Json -Json $Json -SchemaFile $SchemaPath -ErrorAction SilentlyContinue) { return @() }

    # Test-Json only answers true/false (PowerShell 7.4+). Ask the JsonSchema.Net library it is
    # built on for the details; any failure there still reports the file as invalid.
    try {
        $schema = [Json.Schema.JsonSchema]::FromText((Get-Content -LiteralPath $SchemaPath -Raw))
        $options = [Json.Schema.EvaluationOptions]::new()
        $options.OutputFormat = [Json.Schema.OutputFormat]::List
        $results = $schema.Evaluate([System.Text.Json.Nodes.JsonNode]::Parse($Json), $options)
        $details = @(foreach ($detail in $results.Details) {
                if ($detail.Errors) {
                    foreach ($entry in $detail.Errors.GetEnumerator()) {
                        # ToString(): string expansion would join the pointer's segments with spaces.
                        "schema: $($detail.InstanceLocation.ToString()) $($entry.Key): $($entry.Value)"
                    }
                }
            })
        if ($details.Count -gt 0) { return $details }
    }
    catch {
        return @("schema: $($_.Exception.Message)")
    }
    return @("schema: the catalog does not match $(Split-Path -Leaf $SchemaPath)")
}

$problems = [System.Collections.Generic.List[string]]::new()
$json = Get-Content -LiteralPath $CatalogPath -Raw
$appCount = 0

$schemaProblems = @(Get-SchemaProblem $json $SchemaPath)
if ($schemaProblems.Count -gt 0) {
    foreach ($schemaProblem in $schemaProblems) { $problems.Add($schemaProblem) }
}
else {
    # JsonDocument keeps every value as written (ConvertFrom-Json would turn dates into DateTime).
    $document = [System.Text.Json.JsonDocument]::Parse($json)
    try {
        $root = $document.RootElement

        $updatedText = $root.GetProperty('updated').GetString()
        $updated = [datetime]::MinValue
        $isDate = [datetime]::TryParseExact($updatedText, 'yyyy-MM-dd', [cultureinfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::None, [ref] $updated)
        if (-not $isDate) {
            $problems.Add("updated: '$updatedText' is not a valid yyyy-MM-dd date")
        }
        elseif ($updated -gt [datetime]::UtcNow.Date) {
            $problems.Add("updated: $updatedText is in the future")
        }

        $ids = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $names = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($app in $root.GetProperty('apps').EnumerateArray()) {
            $appCount++
            $id = $app.GetProperty('id').GetString()
            $name = $app.GetProperty('name').GetString()
            if (-not $ids.Add($id)) { $problems.Add("apps[$id]: duplicate id") }
            if (-not $names.Add($name)) { $problems.Add("apps[$id]: duplicate name '$name'") }

            $navAllow = [string[]] @($app.GetProperty('navAllow').EnumerateArray() | ForEach-Object { $_.GetString() })
            $startHost = ([uri] $app.GetProperty('startUrl').GetString()).Host
            if (-not (Test-DomainCover $startHost $navAllow)) {
                $problems.Add("apps[$id]: startUrl host '$startHost' is not covered by navAllow")
            }
            foreach ($popup in $app.GetProperty('popupAllow').EnumerateArray()) {
                $popupDomain = $popup.GetString()
                if (-not (Test-DomainCover $popupDomain $navAllow)) {
                    $problems.Add("apps[$id]: popupAllow '$popupDomain' is not covered by navAllow")
                }
            }
        }
    }
    finally {
        $document.Dispose()
    }
}

if ($problems.Count -gt 0) {
    $prefix = if ($env:GITHUB_ACTIONS -eq 'true') { '::error file=catalog/webapps.json::' } else { 'error: ' }
    foreach ($problem in $problems) { Write-Host "$prefix$problem" }
    Write-Host "catalog/webapps.json: $($problems.Count) problem(s)." -ForegroundColor Red
    exit 1
}

Write-Host "catalog/webapps.json: OK ($appCount apps)." -ForegroundColor Green
