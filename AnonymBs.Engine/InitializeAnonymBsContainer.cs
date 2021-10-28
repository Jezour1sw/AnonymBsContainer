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

using Azure.Storage.Blobs;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AnonymBs.Engine
{
    public class InitializeAnonymBsContainer
    {
        private readonly BlobContainerClient _blobContainerClient;
        private readonly DirectoryInfo _directoryInitializeDirPath;
        private readonly int maxParallelUpload = 512;

        public InitializeAnonymBsContainer(
            string connectionString, 
            string containerName,
            string initializeDirPath
        )
        {
            _blobContainerClient = new BlobContainerClient(connectionString: connectionString, blobContainerName: containerName);
             _directoryInitializeDirPath = new DirectoryInfo(initializeDirPath);
        }

        public string GetAccountName()
        {
            return _blobContainerClient.AccountName;
        }

        public HashSet<string> Initialize()
        {
            return Task.Run(() => InitializeAsync()).Result;
        }

        public async Task<HashSet<string>> InitializeAsync()
        {

            HashSet<string> toReturn = new HashSet<string>();

            await _blobContainerClient.CreateIfNotExistsAsync();

            var tasks = new BlockingCollection<Task>(maxParallelUpload);
            int i = 0;
            foreach (var oneFile in _directoryInitializeDirPath.GetFiles())
            {
                i++;
                if(i >= maxParallelUpload)
                {
                    // next iteration
                    await Task.WhenAll(tasks);

                    i = 0;
                }
                tasks.Add(UploadOneFile(_blobContainerClient.GetBlobClient(oneFile.Name), oneFile.FullName));
                toReturn.Add(oneFile.FullName);
            }
            await Task.WhenAll(tasks);

            return toReturn;
        }

        private Task UploadOneFile(BlobClient blob, string fileName)
        {
            return blob.UploadAsync(fileName);
        }
    }
}
