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
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AnonymBs.Engine
{
    public class ClearAnonymBsContainer
    {
        private readonly BlobContainerClient _blobContainerClient;

        public ClearAnonymBsContainer(string connectionString, string containerName)
        {
            _blobContainerClient = new BlobContainerClient(connectionString: connectionString, blobContainerName: containerName);
        }

        public HashSet<string> Clear()
        {
            return Task.Run(() => ClearAsync()).Result;
        }

        public async Task<HashSet<string>> ClearAsync()
        {

            HashSet<string> toReturn = new HashSet<string>();
            await foreach (var oneBlob in _blobContainerClient.GetBlobsAsync())
            {
                _ = _blobContainerClient.DeleteBlobIfExistsAsync(oneBlob.Name);
                toReturn.Add(oneBlob.Name);
            }
            return toReturn;
            
        }
    }
}
