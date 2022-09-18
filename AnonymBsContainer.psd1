@{

    # Script module or binary module file associated with this manifest.
    RootModule             = if ($PSEdition -eq 'Core') { 'Core/AnonymBs.Cmdlets.dll' } else { 'Framework/AnonymBs.Cmdlets.dll' }

    # Version number of this module.
    ModuleVersion          = '1.0.0'

    # Supported PSEditions
    CompatiblePSEditions   = @('Core', 'Desktop')

    # ID used to uniquely identify this module
    GUID                   = '116538a3-fe17-46ec-a093-adfc555a75f1'

    # Author of this module
    Author                 = 'Petr Jezek'

    # Company or vendor of this module
    CompanyName            = '1.SOFTWAROVÁ s.r.o.'

    # Copyright statement for this module
    Copyright              = '2021 1.SOFTWAROVÁ s.r.o. All rights reserved.'

    # Description of the functionality provided by this module
    Description            = 'Anonymization Azure blob storage module'

    # Minimum version of the PowerShell engine required by this module
    PowerShellVersion      = '5.1'

    # Minimum version of Microsoft .NET Framework required by this module. This prerequisite is valid for the PowerShell Desktop edition only.
    DotNetFrameworkVersion = '4.8.0'

    # Functions to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no functions to export.
    FunctionsToExport      = @()

    # Cmdlets to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no cmdlets to export.
    CmdletsToExport        = @(
        'Get-AnonymBsContainerList'
        , 'Clear-AnonymBsContainer'
        , 'Initialize-AnonymBsContainer'
        , 'Convert-AnonymBsContainer'
    )

    # Variables to export from this module
    VariablesToExport      = @()

    # Aliases to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no aliases to export.
    AliasesToExport        = @()

    # Private data to pass to the module specified in RootModule/ModuleToProcess. This may also contain a PSData hashtable with additional module metadata used by PowerShell.
    PrivateData            = @{

        PSData = @{
            ProjectURI = 'https://github.com/Jezour1sw/AnonymBsContainer#readme'
        } # End of PSData hashtable

    } # End of PrivateData hashtable

}

