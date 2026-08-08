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

using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AnonymBs.Engine
{
    public class ConvertAnonymBsContainerProgress
    {
        public string Phase { get; set; } = string.Empty;
        public long TotalItemsToProcess { get; set; }
        public long DiscoveredItems { get; set; }
        public long ProcessedItems { get; set; }
        public long SkippedItems { get; set; }
        public long FailedItems { get; set; }
        public string LastBlobName { get; set; } = string.Empty;
    }

    public class ConvertAnonymBsContainerSummary
    {
        public long TotalItemsToProcess { get; set; }
        public long DiscoveredItems { get; set; }
        public long ProcessedItems { get; set; }
        public long SkippedItems { get; set; }
        public long FailedItems { get; set; }
    }

    public class ConvertAnonymBsContainer
    {
        private readonly BlobContainerClient _sourceBlobContainerClient;
        private readonly BlobContainerClient _targetBlobContainerClient;
        private readonly BlobContainerClient _targetAnonymBlobContainerClient;
        private readonly string _targetConnectionString;
        private readonly string _targetContainerName;
        private readonly string _targetAnonymContainerName;
        private readonly Dictionary<string, Uri> _anonymizedSuffixList = new Dictionary<string, Uri>();
        private readonly bool _isLoadedDefaultSuffix;
        private readonly string _defaultFileSuffix;
        private readonly int _maxParallelConvert;
        private readonly bool _skipIfFileAlreadyExists;
        private string _blobContinuationToken = string.Empty;


        public ConvertAnonymBsContainer(
            string sourceConnectionString,
            string sourceContainerName,
            string targetConnectionString,
            string targetContainerName,
            string targetAnonymContainerName,
            string defaultFileSuffix,
            int maxParallelConvert = 512,
            bool skipIfFileAlreadyExists = true,
            int retryDelayInSeconds = 10,
            int maxNumberOfRetry = 1000
        )
        {
            _targetConnectionString = targetConnectionString;
            _targetContainerName = targetContainerName;
            _targetAnonymContainerName = targetAnonymContainerName;
            _defaultFileSuffix = defaultFileSuffix;
            _maxParallelConvert = maxParallelConvert;
            _skipIfFileAlreadyExists = skipIfFileAlreadyExists;

            BlobClientOptions options = new BlobClientOptions();
            options.Retry.Delay = TimeSpan.FromSeconds(retryDelayInSeconds);
            options.Retry.MaxRetries = maxNumberOfRetry;

            _sourceBlobContainerClient = new BlobContainerClient(connectionString: sourceConnectionString, blobContainerName: sourceContainerName, options: options);
            _targetBlobContainerClient = new BlobContainerClient(connectionString: _targetConnectionString, blobContainerName: _targetContainerName, options: options);
            _targetAnonymBlobContainerClient = new BlobContainerClient(connectionString: _targetConnectionString, blobContainerName: _targetAnonymContainerName, options: options);

            _isLoadedDefaultSuffix = Task.Run(() => InitLoadAnonymFilesDictionaryAsync()).Result;
        }


        private async Task<bool> InitLoadAnonymFilesDictionaryAsync()
        {
            bool isDefaultFileSuffix = false;
            await foreach (var oneAnonymizedBlob in _targetAnonymBlobContainerClient.GetBlobsAsync())
            {
                var suffix = GetSuffixFromBlobName(oneAnonymizedBlob.Name);

                if (suffix.Equals(_defaultFileSuffix))
                    isDefaultFileSuffix = true;

                _anonymizedSuffixList.Add(suffix, new Uri(_targetAnonymBlobContainerClient.Uri.ToString() + '/' + oneAnonymizedBlob.Name));
            }
            return isDefaultFileSuffix;
        }

        public string GetSourceAccountName()
        {
            return _sourceBlobContainerClient.AccountName;
        }

        public string GetTargetAccountName()
        {
            return _targetBlobContainerClient.AccountName;
        }

        public HashSet<string> GetListAnonymizedSuffies()
        {
            return new HashSet<string>(_anonymizedSuffixList.Keys);
        }

        public bool IsLoadedDefaultSuffix()
        {
            return _isLoadedDefaultSuffix;
        }

        public async Task<ConvertAnonymBsContainerSummary> ProcessAllAsync(
            bool skipPreCountingBlobs,
            IProgress<ConvertAnonymBsContainerProgress> progress,
            CancellationToken cancellationToken)
        {
            long totalItemsToProcess = 0;
            if (!skipPreCountingBlobs)
            {
                totalItemsToProcess = await CountTotalItemsAsync(progress, cancellationToken).ConfigureAwait(false);
            }

            long discoveredItems = 0;
            long processedItems = 0;
            long skippedItems = 0;
            long failedItems = 0;

            using (var semaphore = new SemaphoreSlim(_maxParallelConvert))
            {
                var workerTasks = new List<Task>();

                await foreach (var oneBlob in _sourceBlobContainerClient.GetBlobsAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    Interlocked.Increment(ref discoveredItems);
                    ReportProgress(progress, "processing", totalItemsToProcess, discoveredItems, processedItems, skippedItems, failedItems, oneBlob.Name);

                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                    var workerTask = ProcessOneBlobAsync(
                        oneBlob,
                        semaphore,
                        totalItemsToProcess,
                        progress,
                        cancellationToken,
                        () => Interlocked.Increment(ref processedItems),
                        () => Interlocked.Increment(ref skippedItems),
                        () => Interlocked.Increment(ref failedItems),
                        () => discoveredItems,
                        () => processedItems,
                        () => skippedItems,
                        () => failedItems);

                    workerTasks.Add(workerTask);

                    if (workerTasks.Count >= (_maxParallelConvert * 4))
                    {
                        var completedTask = await Task.WhenAny(workerTasks).ConfigureAwait(false);
                        workerTasks.Remove(completedTask);
                        await completedTask.ConfigureAwait(false);
                    }
                }

                await Task.WhenAll(workerTasks).ConfigureAwait(false);
            }

            ReportProgress(progress, "completed", totalItemsToProcess, discoveredItems, processedItems, skippedItems, failedItems, string.Empty);

            return new ConvertAnonymBsContainerSummary
            {
                TotalItemsToProcess = totalItemsToProcess,
                DiscoveredItems = discoveredItems,
                ProcessedItems = processedItems,
                SkippedItems = skippedItems,
                FailedItems = failedItems
            };
        }

        public Task ProcessBatch(WrapperBlobItem wrapperBlobItem)
        {
            var options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = _maxParallelConvert
            };
            var tasks = new ConcurrentBag<Task>();
            Parallel.ForEach(wrapperBlobItem._listBlobItems, options, i =>
            {
                Uri AnonymizedBlobUri = ComputeUriOfAnonymizedBlob(i.Name);
                BlobClient blobClient = _targetBlobContainerClient.GetBlobClient(i.Name);

                // Add to task only when 
                if (!(_skipIfFileAlreadyExists && blobClient.Exists()))
                    tasks.Add(ConvertOneBlob(blobClient, AnonymizedBlobUri));

            });

            return Task.WhenAll(tasks);
        }

        public WrapperBlobItem LoadNextBatchForProcessing()
        {
            var resultSegment = _sourceBlobContainerClient.GetBlobsAsync().AsPages(_blobContinuationToken, pageSizeHint: _maxParallelConvert);

            return Task.Run(() => GetBlobItemBatchAsync(resultSegment)).Result;
        }

        private async Task<WrapperBlobItem> GetBlobItemBatchAsync(IAsyncEnumerable<Page<BlobItem>> onePage)
        {
            List<BlobItem> blobItems = new List<BlobItem>();
            bool isLoadingFinished = true;
            await foreach (Azure.Page<BlobItem> oneBlobItemBatch in onePage)
            {

                blobItems.AddRange(oneBlobItemBatch.Values);
                _blobContinuationToken = oneBlobItemBatch.ContinuationToken;
                if (string.IsNullOrEmpty(_blobContinuationToken))
                    isLoadingFinished = true;
                else
                    isLoadingFinished = false;


                break;
            }
            return new WrapperBlobItem(blobItems, isLoadingFinished: isLoadingFinished);
        }

        private Task ConvertOneBlob(BlobClient blobClient, Uri anonymizedBlobUri)
        {

            return blobClient.StartCopyFromUriAsync(anonymizedBlobUri);
        }

        private async Task<long> CountTotalItemsAsync(IProgress<ConvertAnonymBsContainerProgress> progress, CancellationToken cancellationToken)
        {
            long totalItems = 0;
            await foreach (var oneBlob in _sourceBlobContainerClient.GetBlobsAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                totalItems++;
                if ((totalItems % _maxParallelConvert) == 0)
                {
                    ReportProgress(progress, "counting", totalItems, totalItems, 0, 0, 0, oneBlob.Name);
                }
            }

            ReportProgress(progress, "counting-completed", totalItems, totalItems, 0, 0, 0, string.Empty);
            return totalItems;
        }

        private async Task ProcessOneBlobAsync(
            BlobItem oneBlob,
            SemaphoreSlim semaphore,
            long totalItemsToProcess,
            IProgress<ConvertAnonymBsContainerProgress> progress,
            CancellationToken cancellationToken,
            Action incrementProcessed,
            Action incrementSkipped,
            Action incrementFailed,
            Func<long> getDiscovered,
            Func<long> getProcessed,
            Func<long> getSkipped,
            Func<long> getFailed)
        {
            try
            {
                Uri anonymizedBlobUri = ComputeUriOfAnonymizedBlob(oneBlob.Name);
                BlobClient blobClient = _targetBlobContainerClient.GetBlobClient(oneBlob.Name);

                bool isSkipped = false;
                if (_skipIfFileAlreadyExists)
                {
                    var existsResponse = await blobClient.ExistsAsync(cancellationToken).ConfigureAwait(false);
                    isSkipped = existsResponse.Value;
                }

                if (isSkipped)
                {
                    incrementSkipped();
                }
                else
                {
                    await blobClient.StartCopyFromUriAsync(anonymizedBlobUri, cancellationToken: cancellationToken).ConfigureAwait(false);
                    incrementProcessed();
                }
            }
            catch
            {
                incrementFailed();
                throw;
            }
            finally
            {
                ReportProgress(progress, "processing", totalItemsToProcess, getDiscovered(), getProcessed(), getSkipped(), getFailed(), oneBlob.Name);
                semaphore.Release();
            }
        }

        private static void ReportProgress(
            IProgress<ConvertAnonymBsContainerProgress> progress,
            string phase,
            long totalItemsToProcess,
            long discoveredItems,
            long processedItems,
            long skippedItems,
            long failedItems,
            string lastBlobName)
        {
            progress?.Report(new ConvertAnonymBsContainerProgress
            {
                Phase = phase,
                TotalItemsToProcess = totalItemsToProcess,
                DiscoveredItems = discoveredItems,
                ProcessedItems = processedItems,
                SkippedItems = skippedItems,
                FailedItems = failedItems,
                LastBlobName = lastBlobName ?? string.Empty
            });
        }

        private string GetSuffixFromBlobName(string blobName)
        {
            var splitedBlobname = blobName.Split('.');
            return splitedBlobname[splitedBlobname.Length - 1];
        }

        private Uri ComputeUriOfAnonymizedBlob(string blobName)
        {
            string suffix = GetSuffixFromBlobName(blobName);

            if (!_anonymizedSuffixList.TryGetValue(suffix, out Uri toReturn))
            {
                _anonymizedSuffixList.TryGetValue(_defaultFileSuffix, out Uri defaultAnonymized);
                toReturn = defaultAnonymized;
            }
            return toReturn;

        }

    }
}
