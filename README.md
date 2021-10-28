# Powershell CmdLet to enable Anonymization of blobs

[![Build](https://github.com/Jezour1sw/AnonymBsContainer/actions/workflows/main.yml/badge.svg)](https://github.com/Jezour1sw/AnonymBsContainer/actions/workflows/main.yml)

## Description

The module is implementing a few CmdLet to help anonymize blobs on Azure blob storage in a specific container.
Typical use is for situations when Data for production system are stored on Azure blob storage "A",
but we need to copy these data into Non-production storage "B". E.g. Staging, UAT, etc.

In such a scenario is required to create the same structure of blobs on the target container, but the content of blobs should be anonymized.
However, each anonymized file is required to keep the expected content based on the suffix. e.g. abc.docx file should be possible open in word format. abc.jpg should be a picture in the proper format. etc.

To enable such functionality seems to be a good idea to keep somewhere a list of already anonymized templates files/blobs.

## List of command

### Clear-AnonymBsContainer

This command is handy while the testing of the functionality, but the same functionality can be found anywhere (e.g. AzCopy.exe )

#### Example of Clear-AnonymBsContainer

``` pwsh
Clear-AnonymizeAzBs `
  -ConnectionString 'DefaultEndpointsProtocol=https;AccountName=MyTarget;AccountKey=xxx;EndpointSuffix=core.windows.net' `
  -ContainerName documents
```

### Initialize-AnonymBsContainer

This command is just loading anonymization templates from the local folder to the target blob storage container.

#### Examples of Initialize-AnonymBsContainer

``` pwsh
Initialize-AnonymBsContainer `
-ConnectionString 'DefaultEndpointsProtocol=https;AccountName=MyTarget;AccountKey=Target;EndpointSuffix=core.windows.net' `
-ContainerName 'anonymized-documents' `
-Verbose
```

Without any preferences to your own list of templates from anonymization, you can use the default one, but it's just a basic list.
You can define and initialize via this command upload into the container with anonymization templates your own.
Note: You're more than welcome to contribute some files into this project.
In the meantime, you can use parameter **InitializeDirPath** to define the directory where are defined your own anonymization templates.
Just please keep in mind that only one file is expected for each suffix.

``` pwsh
Initialize-AnonymBsContainer `
-ConnectionString 'DefaultEndpointsProtocol=https;AccountName=MyTarget;AccountKey=Target;EndpointSuffix=core.windows.net' `
-ContainerName 'anonymized-documents' `
-InitializeDirPath c:\temp\my-anonymization-templates-documents
-Verbose
```

### Convert-AnonymBsContainer

This command is the reading list of blobs from the source container. For each file is trying to get proper suffix from the container with anonymization templates.
When such suffix doesn't exist in the container of templates. The default one will be chosen.

#### Examples of Convert-AnonymBsContainer

``` pwsh
Convert-AnonymBsContainer `
-SourceConnectionString 'DefaultEndpointsProtocol=https;AccountName=MySource;AccountKey=xxx;EndpointSuffix=core.windows.net' `
-SourceContainerName 'documents' `
-TargetConnectionString 'DefaultEndpointsProtocol=https;AccountName=MyTarget;AccountKey=xxx;EndpointSuffix=core.windows.net' `
-TargetContainerName 'documents' `
-TargetAnonymContainerName 'anonymized-documents' 
```

For each blob where the same suffix doesn't exist in the anonymization templates, the default suffix will be chosen the 'size0' or custom value by the parameter **DefaultFileSuffix**.
In this case the file **a.size0**. In such a case, the blob name remains the same as the original content of the blob will be according to the file **a.size0** 0 bytes.

``` pwsh
Convert-AnonymBsContainer `
-SourceConnectionString 'DefaultEndpointsProtocol=https;AccountName=MySource;AccountKey=xxx;EndpointSuffix=core.windows.net' `
-SourceContainerName 'documents' `
-TargetConnectionString 'DefaultEndpointsProtocol=https;AccountName=MyTarget;AccountKey=xxx;EndpointSuffix=core.windows.net' `
-TargetContainerName 'documents' `
-TargetAnonymContainerName 'anonymized-documents' `
-DefaultFileSuffix 'size0' `
-Verbose
```

We usually need to anonymize a lot of blobs. It expects the proper speed of processing and it's possible only via async processing of batches.
To enable set the size batch is possible to override the default batch size 100 via parameter **MaxParallelDownloads**

``` pwsh
Convert-AnonymBsContainer `
-SourceConnectionString 'DefaultEndpointsProtocol=https;AccountName=MySource;AccountKey=xxx;EndpointSuffix=core.windows.net' `
-SourceContainerName 'documents' `
-TargetConnectionString 'DefaultEndpointsProtocol=https;AccountName=MyTarget;AccountKey=xxx;EndpointSuffix=core.windows.net' `
-TargetContainerName 'documents' `
-TargetAnonymContainerName 'anonymized-documents' `
-MaxParallelDownloads 50
```

Sometimes is required to skip already existing files to speed up the processing via parameter **SkipIfFileAlreadyExists**. The default is true

``` pwsh
Convert-AnonymBsContainer `
-SourceConnectionString 'DefaultEndpointsProtocol=https;AccountName=MySource;AccountKey=xxx;EndpointSuffix=core.windows.net' `
-SourceContainerName 'documents' `
-TargetConnectionString 'DefaultEndpointsProtocol=https;AccountName=MyTarget;AccountKey=xxx;EndpointSuffix=core.windows.net' `
-TargetContainerName 'documents' `
-TargetAnonymContainerName 'anonymized-documents' `
-SkipIfFileAlreadyExists:$true
```

Sometimes is not required to see a list of all files. To get such information should be set the parameter **ShowEachFileName** to true. The default is false
Note: Names of files are visible only with **-Debug** flag.

``` pwsh
Convert-AnonymBsContainer `
-SourceConnectionString 'DefaultEndpointsProtocol=https;AccountName=MySource;AccountKey=xxx;EndpointSuffix=core.windows.net' `
-SourceContainerName 'documents' `
-TargetConnectionString 'DefaultEndpointsProtocol=https;AccountName=MyTarget;AccountKey=xxx;EndpointSuffix=core.windows.net' `
-TargetContainerName 'documents' `
-TargetAnonymContainerName 'anonymized-documents' `
-ShowEachFileName:$true
```

## For contributors

The build is via execution **./build.ps1** script.

## Special thanks

Thanks to this article <https://docs.microsoft.com/en-us/powershell/scripting/dev-cross-plat/resolving-dependency-conflicts?view=powershell-7.1>
and the related GIT repo <https://github.com/rjmholt/ModuleDependencyIsolationExample> where is the exact example by Rob <https://github.com/rjmholt>
