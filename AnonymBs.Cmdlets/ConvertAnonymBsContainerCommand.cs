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
using System.Threading;
using System.Threading.Tasks;

namespace AnonymBs.Cmdlets
{
    [Cmdlet(VerbsData.Convert, "AnonymBsContainer")]
    public class ConvertAnonymBsContainerCommand : PSCmdlet
    {
        private ConvertAnonymBsContainer _copyAnonymBsContainer;
        private Stopwatch _swTotal = new Stopwatch();
        private ProgressRecord _progressRecord;
        private ConvertAnonymBsContainerProgress _lastProgressSnapshot = new ConvertAnonymBsContainerProgress();
        private readonly object _progressSync = new object();

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
        [ValidateRange(1, 10000)]
        public int MaxParallelDownloads = 800;

        [Parameter(
            Position = 7,
            Mandatory = false,
            HelpMessage = "When it is true than is skipped test if file exists and it simply override the file. When is true it should be quicker. Default is true. "
        )]
        public bool SkipIfFileAlreadyExists = true;

        [Parameter(
            Position = 8,
            Mandatory = false,
            HelpMessage = "When is required to get info about each anonymized file via debug messages. Default is false. "
        )]
        public bool ShowEachFileName = false;

        [Parameter(
            Position = 9,
            Mandatory = false,
            HelpMessage = "When is set the progress will not display percentage due to missing count, but processing is quicker. "
        )]
        public bool SkipPreCountingBlobs = false;

        [Parameter(
            Position = 10,
            Mandatory = false,
            HelpMessage = "When is set the delay between retry is according this value. Default is 10 secounds. "
        )]
        public int RetryDelayInSeconds = 10;

        [Parameter(
            Position = 11,
            Mandatory = false,
            HelpMessage = "When is set the number of retry is according this value. Default is 1000."
        )]
        public int MaxNumberOfRetry = 1000;

        [Parameter(
            Position = 12,
            Mandatory = false,
            HelpMessage = "Heartbeat interval in seconds for progress logging during long-running operations. Default is 30 seconds."
        )]
        [ValidateRange(5, 600)]
        public int HeartbeatIntervalInSeconds = 30;


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
                SkipIfFileAlreadyExists,
                RetryDelayInSeconds,
                MaxNumberOfRetry
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
            WriteVerbose($"SkipPreCountingBlobs: [{SkipPreCountingBlobs}]");
            WriteVerbose($"HeartbeatIntervalInSeconds: [{HeartbeatIntervalInSeconds}]");


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
            TimeSpan heartbeatInterval = TimeSpan.FromSeconds(HeartbeatIntervalInSeconds);
            string lastEmittedBlobName = string.Empty;

            var progressReporter = new Progress<ConvertAnonymBsContainerProgress>(snapshot =>
            {
                lock (_progressSync)
                {
                    _lastProgressSnapshot = snapshot;
                }
            });

            Task<ConvertAnonymBsContainerSummary> processTask = _copyAnonymBsContainer.ProcessAllAsync(SkipPreCountingBlobs, progressReporter, CancellationToken.None);
            while (!processTask.Wait(heartbeatInterval))
            {
                ConvertAnonymBsContainerProgress snapshot;
                lock (_progressSync)
                {
                    snapshot = _lastProgressSnapshot;
                }

                if (ShowEachFileName && !string.IsNullOrWhiteSpace(snapshot.LastBlobName) && !string.Equals(lastEmittedBlobName, snapshot.LastBlobName, StringComparison.Ordinal))
                {
                    lastEmittedBlobName = snapshot.LastBlobName;
                    WriteDebug(snapshot.LastBlobName);
                }

                WriteProgressFromSnapshot(snapshot, isCompleted: false);
            }

            ConvertAnonymBsContainerSummary summary = processTask.GetAwaiter().GetResult();

            lock (_progressSync)
            {
                _lastProgressSnapshot = new ConvertAnonymBsContainerProgress
                {
                    Phase = "completed",
                    TotalItemsToProcess = summary.TotalItemsToProcess,
                    DiscoveredItems = summary.DiscoveredItems,
                    ProcessedItems = summary.ProcessedItems,
                    SkippedItems = summary.SkippedItems,
                    FailedItems = summary.FailedItems,
                    LastBlobName = string.Empty
                };
            }

            WriteProgressFromSnapshot(_lastProgressSnapshot, isCompleted: true);

            long doneItems = summary.ProcessedItems + summary.SkippedItems;
            WriteVerbose($"Total: [Discovered={summary.DiscoveredItems}, Processed={summary.ProcessedItems}, Skipped={summary.SkippedItems}, Failed={summary.FailedItems}, Elapsed={_swTotal.Elapsed}, Items per Seconds:{(doneItems / _swTotal.Elapsed.TotalSeconds)}]");
        }

        private void WriteProgressFromSnapshot(ConvertAnonymBsContainerProgress snapshot, bool isCompleted)
        {
            long totalDone = snapshot.ProcessedItems + snapshot.SkippedItems;
            string operation = $"Phase={snapshot.Phase}, Discovered={snapshot.DiscoveredItems}, Processed={snapshot.ProcessedItems}, Skipped={snapshot.SkippedItems}, Failed={snapshot.FailedItems}, Elapsed={_swTotal.Elapsed}";

            if (!string.IsNullOrWhiteSpace(snapshot.LastBlobName) && ShowEachFileName)
            {
                operation += $", Blob={snapshot.LastBlobName}";
            }

            _progressRecord.CurrentOperation = operation;

            if (snapshot.TotalItemsToProcess > 0)
            {
                int percentageComplete = (int)((totalDone * 100) / snapshot.TotalItemsToProcess);
                if (percentageComplete > 100)
                {
                    percentageComplete = 100;
                }
                _progressRecord.PercentComplete = percentageComplete;
            }

            if (isCompleted)
            {
                _progressRecord.PercentComplete = 100;
                _progressRecord.RecordType = ProgressRecordType.Completed;
            }

            WriteProgress(_progressRecord);
            WriteDebug($"Heartbeat: {operation}");
            WriteVerbose($"Heartbeat: {operation}");
        }


        protected override void EndProcessing()
        {
            WriteDebug("stop watch");
            _swTotal.Stop();
            WriteVerbose($"Time: [Elapsed Days:{_swTotal.Elapsed.TotalDays}, Hours:{_swTotal.Elapsed.TotalHours}, Minutes: {_swTotal.Elapsed.TotalMinutes}, Seconds: {_swTotal.Elapsed.TotalSeconds}, Milliseconds: {_swTotal.Elapsed.TotalMilliseconds}]");
        }
    }
}