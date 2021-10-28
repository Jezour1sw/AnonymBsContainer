/*
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
*/

using AnonymBs.Engine;
using System.IO;
using System.Management.Automation;

namespace AnonymBs.Cmdlets
{
    [Cmdlet(VerbsData.Initialize, "AnonymBsContainer")]
    public class InitializeAnonymBsContainerCommand : PSCmdlet
    {
        private InitializeAnonymBsContainer _initializeAnonymBsContainer;

        [Parameter(
            Position = 0,
            Mandatory = true,
            HelpMessage = "The Target Connection string where is required to upload anonymization templates of suffixes to anonymize."
        )]
        [ValidateNotNullOrEmpty]
        public string ConnectionString { get; set; }

        [Parameter(
            Position = 1,
            Mandatory = true,
            HelpMessage = "The Target Container where is required to upload anonymization templates of suffixes to anonymize."
        )]
        [ValidateNotNullOrEmpty]
        public string ContainerName { get; set; } = "default-anonymized-documents";

        [Parameter(
            Position = 2,
            Mandatory = false,
            HelpMessage = "The path on local disk where is files e.g. a.jpg, a.jpeg, a.gif, a.docx, .. temaplates for anonymization blobs base on the suffix. When is the parameter the InitializeDirPath empty. There's few default files for anonymation is part of the module."
        )]
        [Parameter(Mandatory = false)]
        public string InitializeDirPath { get; set; }


        protected override void BeginProcessing()
        {
            if(string.IsNullOrEmpty(InitializeDirPath))
            {
                InitializeDirPath = Path.Combine(MyInvocation.MyCommand.Module.ModuleBase, "default-anonymized-documents");
                
            }
            if (!Directory.Exists(InitializeDirPath))
            {
                ParameterBindingException pbe = new ParameterBindingException($"The directory with anonymized files doesn't exists [{InitializeDirPath}]");
                ErrorRecord erec = new ErrorRecord(pbe, null, ErrorCategory.PermissionDenied, InitializeDirPath);
                ThrowTerminatingError(erec);
            }
            WriteVerbose($"path to folder on local for upload anonymized documents: [{InitializeDirPath}]");
            WriteVerbose($"Container name: [{ContainerName}]");
            _initializeAnonymBsContainer = new InitializeAnonymBsContainer(ConnectionString, ContainerName, InitializeDirPath);
            WriteVerbose($"Account name: [{_initializeAnonymBsContainer.GetAccountName()}]");
        }


        protected override void ProcessRecord()
        {
            foreach (var blobName in _initializeAnonymBsContainer.Initialize())
            {
                WriteVerbose(blobName);
            }
        }

        protected override void EndProcessing()
        {
            

        }
    }
}