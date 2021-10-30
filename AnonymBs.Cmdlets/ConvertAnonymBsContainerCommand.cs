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
using System;
using System.Diagnostics;
using System.Management.Automation;

namespace AnonymBs.Cmdlets
{
    [Cmdlet(VerbsData.Convert, "AnonymBsContainer")]
    public class ConvertAnonymBsContainerCommand : PSCmdlet
    {
        private ConvertAnonymBsContainer _copyAnonymBsContainer;
        private Stopwatch _swTotal = new Stopwatch();
        private ProgressRecord _progressRecord;

        [Parameter(
            Position = 0,
            Mandatory = true,
            HelpMessage = "The Source Connection string to access of source Azure Blob Storage."
        )]
        [ValidateNotNullOrEmpty]
        public string SourceConnectionString;

        [Parameter(
            Position = 1,
            Mandatory = true,
            HelpMessage = "The Source container name of Azure Blob Storage."
        )]
        [ValidateNotNullOrEmpty]
        public string SourceContainerName;

        [Parameter(
            Position = 2,
            Mandatory = true,
            HelpMessage = "The Target Connection string to access of tource Azure Blob Storage."
        )]
        [ValidateNotNullOrEmpty]
        public string TargetConnectionString;


        [Parameter(
            Position = 3,
            Mandatory = true,
            HelpMessage = "The Target container name of Azure Blob Storage."
        )]
        [ValidateNotNullOrEmpty]
        public string TargetContainerName;

        [Parameter(
            Position = 4,
            Mandatory = true,
            HelpMessage = "The Anonym Container name in target storage of Azure Blob Storage e.g. data-refresh-anonymized-documents."
        )]
        [ValidateNotNullOrEmpty]
        public string TargetAnonymContainerName;


        [Parameter(
            Position = 5,
            Mandatory = false,
            HelpMessage = "When the suffix of file for anonymization is not in AnonymizedSuffixList, than it will be handled as the DefaultFileSuffix. e.g. size0 "
        )]
        public string DefaultFileSuffix = "size0";

        [Parameter(
            Position = 6,
            Mandatory = false,
            HelpMessage = "The number of concurrent threas to copy files (Range 1.. 5000). Default is 512."
        )]
        [ValidateRange(1, 5000)]
        public int MaxParallelDownloads = 100;

        [Parameter(
            Position = 7,
            Mandatory = false,
            HelpMessage = "When it is true than is skipped test if file exists and it simply override the file. When is true it should be quicker. Default is true. "
        )]
        public bool SkipIfFileAlreadyExists = true;

        [Parameter(
            Position = 8,
            Mandatory = false,
            HelpMessage = "When is reuired to get info about each anonymized file. Default is false. "
        )]
        public bool ShowEachFileName = false;


        protected override void BeginProcessing()
        {
            
            WriteDebug("start watch");
            _swTotal.Start();
            
            _copyAnonymBsContainer = new ConvertAnonymBsContainer(
                SourceConnectionString,
                SourceContainerName,
                TargetConnectionString,
                TargetContainerName,
                TargetAnonymContainerName,
                DefaultFileSuffix,
                MaxParallelDownloads,
                SkipIfFileAlreadyExists
            );
            WriteVerbose($"Source account name: [{_copyAnonymBsContainer.GetSourceAccountName()}]");
            WriteVerbose($"Source container name: [{SourceContainerName}]");
            WriteVerbose($"Target account name: [{_copyAnonymBsContainer.GetTargetAccountName()}]");
            WriteVerbose($"Target container name: [{TargetContainerName}]");
            WriteVerbose($"Target anonym container name: [{TargetAnonymContainerName}]");
            WriteVerbose($"List of anonymized suffix list: [{string.Join<string>(",", _copyAnonymBsContainer.GetListAnonymizedSuffies())}]");
            WriteVerbose($"Is loaded default suffix: [{_copyAnonymBsContainer.IsLoadedDefaultSuffix()}]");
            WriteVerbose($"DefaultFileSuffix: [{DefaultFileSuffix}]");
            WriteVerbose($"MaxParallelDownloads: [{MaxParallelDownloads}]");
            WriteVerbose($"SkipIfFileAlreadyExists: [{SkipIfFileAlreadyExists}]");
            WriteVerbose($"ShowEachFileName: [{ShowEachFileName}]");

            if (!_copyAnonymBsContainer.IsLoadedDefaultSuffix())
            {
                ParameterBindingException pbe = new ParameterBindingException($"The default suffix is missing in the container on target as source for anonymization [{DefaultFileSuffix}]. List of anonymized suffixes is [{string.Join<string>(",", _copyAnonymBsContainer.GetListAnonymizedSuffies())}]");
                ErrorRecord erec = new ErrorRecord(pbe, null, ErrorCategory.PermissionDenied, DefaultFileSuffix);
                ThrowTerminatingError(erec);
            }

            _progressRecord = new ProgressRecord(
                0,
                "Convert/Copy anonymized blobs",
                $"[account: {_copyAnonymBsContainer.GetSourceAccountName()}, container: {SourceContainerName}] => [account: {_copyAnonymBsContainer.GetTargetAccountName()}, container: {TargetContainerName}] with anonym file templates in [account: {_copyAnonymBsContainer.GetTargetAccountName()}, container: {TargetAnonymContainerName}]"
            );

        }

        
        protected override void ProcessRecord()
        { 

            long totalProcessedItemCounter = 0;

            Stopwatch swCounterOfItems = new Stopwatch();
            swCounterOfItems.Start();
            WriteVerbose("Computing number of items to process...");

            long totalItemCounter = _copyAnonymBsContainer.TotalCounter();

            swCounterOfItems.Stop();
            WriteVerbose($"Items to process: {totalItemCounter} [Counting time of items to process: {swCounterOfItems.Elapsed}]");

            bool isLoadingFinished;
            do
            {
                WrapperBlobItem wrapperBlobItem = _copyAnonymBsContainer.LoadNextBatchForProcessing();

                if (ShowEachFileName)
                {
                    foreach (var oneBlobName in wrapperBlobItem.GetNames())
                    {
                        WriteDebug(oneBlobName);
                    }
                }


                Stopwatch swIncrement = new Stopwatch();
                swIncrement.Start();

                _copyAnonymBsContainer.ProcessBatch(wrapperBlobItem);

                swIncrement.Stop();
                var incrementItemCounter = wrapperBlobItem.Count();
                totalProcessedItemCounter += incrementItemCounter;
                int percentageComplete = (int)((totalProcessedItemCounter * 100) / totalItemCounter);

                WriteVerbose(string.Format("Progress: [Increment items {0}, Elapsed={1}, Files per Seconds:{2}], [Total items {3}, Elapsed:{4}, Files per Seconds:{5}] {6}% [{7}/{8}]",
                    incrementItemCounter, swIncrement.Elapsed, (incrementItemCounter / swIncrement.Elapsed.TotalSeconds),
                    totalProcessedItemCounter, _swTotal.Elapsed, (totalProcessedItemCounter / _swTotal.Elapsed.TotalSeconds),
                    percentageComplete, totalProcessedItemCounter, totalItemCounter
                    ));


                if (percentageComplete >= 100)
                {
                    _progressRecord.PercentComplete = 100;
                    _progressRecord.RecordType = ProgressRecordType.Completed;
                }
                else
                {
                    _progressRecord.PercentComplete = percentageComplete;
                }


                WriteProgress(_progressRecord);

                isLoadingFinished = wrapperBlobItem.IsLoadingFinished();

            }
            while (!isLoadingFinished);

            _progressRecord.PercentComplete = 100;
            _progressRecord.RecordType = ProgressRecordType.Completed;

            WriteProgress(_progressRecord);

            WriteVerbose(String.Format("Total: [{0}, Elapsed={1}, Files per Seconds:{2}]", 
                totalProcessedItemCounter, _swTotal.Elapsed, (totalProcessedItemCounter / _swTotal.Elapsed.TotalSeconds)));

        }
        

        protected override void EndProcessing()
        {
            WriteDebug("stop watch");
            _swTotal.Stop();
            WriteVerbose(String.Format("Time: [Elapsed Days:{0}, Hours:{1}, Minutes: {2}, Seconds: {3}, Milliseconds: {4}]",
                _swTotal.Elapsed.TotalDays,
                _swTotal.Elapsed.TotalHours,
                _swTotal.Elapsed.TotalMinutes,
                _swTotal.Elapsed.TotalSeconds,
                _swTotal.Elapsed.TotalMilliseconds));
        }
    }
}