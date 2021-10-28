<#
    Copyright 2021 Petr Jezek, 1.SOFTWAROVÁ s.r.o.

    Permission to use, copy, modify, and distribute this software for any
    purpose with or without fee is hereby granted, provided that the above
    copyright notice and this permission notice appear in all copies.

    THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
    WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
    MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
    ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
    WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
    ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
    OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
#>

param(
    [ValidateSet('Debug', 'Release')]
    [string]
    $Configuration = 'Debug'
)

$netCore = 'netcoreapp3.1'
$netFramework = 'net461'
$moduleName = 'AnonymBs'
$moduleNameFull = 'AnonymBsContainer'

$outPath = "$PSScriptRoot/out/$moduleNameFull"
$commonPath = "$outPath/Common"
$corePath = "$outPath/Core"
$frameworkPath = "$outPath/Framework"
$anonymizedDocumentsPath = "$outPath/default-anonymized-documents"

# hard remove
if (Test-Path "$PSScriptRoot/$moduleName.Cmdlets/bin") {
    Remove-Item -Path "$PSScriptRoot/$moduleName.Cmdlets/bin" -Recurse
}
#if (Test-Path "$PSScriptRoot/$moduleName.Cmdlets/obj")
#{
#    Remove-Item -Path "$PSScriptRoot/$moduleName.Cmdlets/obj" -Recurse
#}

# hard remove
if (Test-Path "$PSScriptRoot/$moduleName.Engine/bin") {
    Remove-Item -Path "$PSScriptRoot/$moduleName.Engine/bin" -Recurse
}
#if (Test-Path "$PSScriptRoot/$moduleName.Engine/obj")
#{
#    Remove-Item -Path "$PSScriptRoot/$moduleName.Engine/obj" -Recurse
#}

if (Test-Path $outPath) {
    Remove-Item -Path $outPath -Recurse
}

New-Item -Path $outPath -ItemType Directory
New-Item -Path $commonPath -ItemType Directory
New-Item -Path $corePath -ItemType Directory
New-Item -Path $frameworkPath -ItemType Directory
New-Item -Path $anonymizedDocumentsPath -ItemType Directory


Push-Location "$PSScriptRoot/$moduleName.Engine"
try {
    dotnet publish -c $Configuration
}
finally {
    Pop-Location
}

Push-Location "$PSScriptRoot/$moduleName.Cmdlets"
try {
    dotnet publish -f $netCore -c $Configuration
    dotnet publish -f $netFramework -c $Configuration
}
finally {
    Pop-Location
}

Copy-Item -Path "$PSScriptRoot/default-anonymized-documents" -Destination $outPath -Recurse -Force


Copy-Item -Path "$PSScriptRoot/$moduleNameFull.psd1" -Destination $outPath


# Create list of files as HashSet.
$commonFiles = [System.Collections.Generic.HashSet[string]]::new()

# Primary copy files form the netstandard2.0
Get-ChildItem -Path "$PSScriptRoot/$moduleName.Engine/bin/$Configuration/netstandard2.0/publish" |
Where-Object { $_.Extension -in '.dll', '.pdb' } |
ForEach-Object { [void]$commonFiles.Add($_.Name); Copy-Item -LiteralPath $_.FullName -Destination $commonPath }
	
# Secondary copy files form the core
Get-ChildItem -Path "$PSScriptRoot/$moduleName.Cmdlets/bin/$Configuration/$netCore/publish" |
Where-Object { $_.Extension -in '.dll', '.pdb' -and -not $commonFiles.Contains($_.Name) } |
ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $corePath }
	
# 3rd in the row copy files form the net461
Get-ChildItem -Path "$PSScriptRoot/$moduleName.Cmdlets/bin/$Configuration/$netFramework/publish" |
Where-Object { $_.Extension -in '.dll', '.pdb' -and -not $commonFiles.Contains($_.Name) } |
ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $frameworkPath }

Write-Host "List the result dir"
Get-ChildItem -Path $outPath -Recurse