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
$moduleName = 'AnonymBsContainer'
$nuget = 'nuget.exe'
$nuget = 'C:\Users\A\Downloads\nuget.exe'
$outPath = "$PSScriptRoot/out/$moduleName"
$nugetDir = "$PSScriptRoot/out/nuget/"
if (Test-Path $nugetDir) {
    Remove-Item -Path $nugetDir -Recurse
}

New-Item -Path $nugetDir -ItemType Directory


$moduleVersion = '1.0.0' #$Env:VersionVariable
$moduleManifestFile = "$outPath/$moduleName.psd1"
$nuspecFullName = "$outPath/$moduleName.nuspec"
$moduleTagModule = 'PSModule'
$moduleTagIncludesFunction = 'PSIncludes_Function'
$moduleTagFunction = 'PSFunction'
$moduleTagCommand = 'PSCommand'

$moduleManifest = Test-ModuleManifest -Path $moduleManifestFile
$moduleDescription = $moduleManifest.Description
$moduleAuthor = $moduleManifest.Author
$moduleOwner = $moduleManifest.CompanyName
$moduleCopyright = $moduleManifest.Copyright

$moduleTagsList = New-Object System.Collections.ArrayList
$moduleTagsList.Add($moduleTagModule)

if ($moduleManifest.ExportedFunctions -and $moduleManifest.ExportedFunctions.Count -gt 0) {
    $null = $moduleTagsList.Add($moduleTagIncludesFunction)
}

$moduleManifest.ExportedFunctions.GetEnumerator() | ForEach-Object {
    $key = $_.Key
    $tagTemplate = '{0}_{1}'
    $tagFunc = $tagTemplate -f $moduleTagFunction, $key
    $tagCmd = $tagTemplate -f $moduleTagCommand, $key
    $null = $moduleTagsList.Add($tagFunc)
    $null = $moduleTagsList.Add($tagCmd)
}

$moduleTagsString = $moduleTagsList -join ' '
$metaDataElementsHash = [ordered]@{
    id                       = $moduleName
    version                  = $moduleVersion
    description              = $moduleDescription
    authors                  = $moduleAuthor
    owners                   = $moduleOwner
    releaseNotes             = $ReleaseNotes
    requireLicenseAcceptance = 'false'
    copyright                = $moduleCopyright
    tags                     = $moduleTagsString
}

#create nuspec file
$nameSpaceUri = "http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"
[xml]$xml = New-Object System.Xml.XmlDocument

$xmlDeclaration = $xml.CreateXmlDeclaration("1.0", "utf-8", $null)
$xml.AppendChild($xmlDeclaration) | Out-Null

#create top-level elements
$packageElement = $xml.CreateElement("package", $nameSpaceUri)
$metaDataElement = $xml.CreateElement("metadata", $nameSpaceUri)

foreach ($key in $metaDataElementsHash.Keys) {
    $element = $xml.CreateElement($key, $nameSpaceUri)
    $elementInnerText = $metaDataElementsHash.item($key)
    $element.InnerText = $elementInnerText
    $metaDataElement.AppendChild($element) | Out-Null
}

#required modules
if ($moduleManifest.RequiredModules -and $moduleManifest.RequiredModules.Count -gt 0) {
    $dependenciesElement = $xml.CreateElement("dependencies", $nameSpaceUri)

    $moduleManifest.RequiredModules.GetEnumerator() | ForEach-Object {
        $moduleDependency = $_        

        $element = $xml.CreateElement("dependency", $nameSpaceUri)
        $element.SetAttribute("id", $moduleDependency.Name)
        if ($moduleDependency.Version) { $element.SetAttribute("version", $moduleDependency.Version.ToString()) }

        $dependenciesElement.AppendChild($element) | Out-Null
    }

    $metaDataElement.AppendChild($dependenciesElement) | Out-Null
}

$packageElement.AppendChild($metaDataElement) | Out-Null
$xml.AppendChild($packageElement) | Out-Null

#save nuspec file
$xml.save($nuspecFullName)

#create package
. $nuget pack $nuspecFullName -OutputDirectory $nugetDir