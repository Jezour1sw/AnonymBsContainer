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

using Azure.Storage.Blobs.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnonymBs.Engine
{
    public class WrapperBlobItem
    {
        public readonly IReadOnlyList<BlobItem> _listBlobItems = new List<BlobItem>();
        private readonly bool _isLoadingFinished;

        public WrapperBlobItem(IReadOnlyList<BlobItem> listBlobItems, bool isLoadingFinished)
        {
            _listBlobItems = listBlobItems;
            _isLoadingFinished = isLoadingFinished;
        }

        public bool IsEmpty()
        {
            return (_listBlobItems.Count == 0);
        }

        public Int64 Count()
        {
            return _listBlobItems.Count;
        }

        public IReadOnlyList<string> GetNames()
        {
            return _listBlobItems.Select(x => x.Name).ToList();
        }

        public bool IsLoadingFinished()
        {
            return _isLoadingFinished;
        }
    }
}
