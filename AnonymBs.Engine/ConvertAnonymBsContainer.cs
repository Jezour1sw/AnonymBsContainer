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
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AnonymBs.Engine
{
    public class ConvertAnonymBsContainer
    {
        private readonly BlobContainerClient _sourceBlobContainerClient;
        private readonly BlobContainerClient _targetBlobContainerClient;
        private readonly BlobContainerClient _targetAnonymBlobContainerClient;
        private readonly string _targetConnectionString;
        private readonly string _targetContainerName;
        private readonly string _targetAnonymContainerName;
        private readonly Dictionary<string,Uri> _anonymizedSuffixList = new Dictionary<string, Uri>();
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
            bool skipIfFileAlreadyExists = true
        )
        {
            _targetConnectionString = targetConnectionString;
            _targetContainerName = targetContainerName;
            _targetAnonymContainerName = targetAnonymContainerName;
            _defaultFileSuffix = defaultFileSuffix;
            _maxParallelConvert = maxParallelConvert;
            _skipIfFileAlreadyExists = skipIfFileAlreadyExists;


            _sourceBlobContainerClient = new BlobContainerClient(connectionString: sourceConnectionString, blobContainerName: sourceContainerName);
            _targetBlobContainerClient = new BlobContainerClient(connectionString: _targetConnectionString, blobContainerName: _targetContainerName);
            _targetAnonymBlobContainerClient = new BlobContainerClient(connectionString: _targetConnectionString, blobContainerName: _targetAnonymContainerName);

            _isLoadedDefaultSuffix = Task.Run(() => InitLoadAnonymFilesDictionaryAsync()).Result;
        }
        

        private async Task<bool> InitLoadAnonymFilesDictionaryAsync()
        {
            bool isDefaultFileSuffix = false;
            await foreach(var oneAnonymizedBlob in _targetAnonymBlobContainerClient.GetBlobsAsync())
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

        /*
        public long TotalCounter()
        {
            return Task.Run(() => TotalCounterAsync()).Result;
        }

        public async Task<long> TotalCounterAsync()
        {
            long toReturn = 0;
            string continuationCounterToken = string.Empty;
            await foreach (var oneBlobPage in _sourceBlobContainerClient.GetBlobsAsync().AsPages(continuationToken: continuationCounterToken, pageSizeHint: _maxParallelConvert))
            {
                continuationCounterToken = oneBlobPage.ContinuationToken;
                toReturn += oneBlobPage.Values.Count;
            }
            return toReturn;
        }
        */

        public void ProcessBatch(WrapperBlobItem wrapperBlobItem)
        {
            var options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = _maxParallelConvert
            };
            var tasks = new BlockingCollection<Task>(_maxParallelConvert);
            Parallel.ForEach(wrapperBlobItem._listBlobItems, options, i =>
            {
                Uri AnonymizedBlobUri = ComputeUriOfAnonymizedBlob(i.Name);
                BlobClient blobClient = _targetBlobContainerClient.GetBlobClient(i.Name);

                // Add to task only when 
                if (!(_skipIfFileAlreadyExists && blobClient.Exists()))
                    tasks.Add(ConvertOneBlob(blobClient, AnonymizedBlobUri));

            });

            Task allTasks = Task.WhenAll(tasks);
            allTasks.Wait();
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

        private string GetSuffixFromBlobName(string blobName)
        {
            var splitedBlobname = blobName.Split('.');
            return splitedBlobname[splitedBlobname.Length-1];
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
