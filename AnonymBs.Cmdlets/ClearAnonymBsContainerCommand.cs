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
using System.Management.Automation;

namespace AnonymBs.Cmdlets
{
    [Cmdlet(VerbsCommon.Clear, "AnonymBsContainer")]
    public class ClearAnonymBsContainerCommand : PSCmdlet
    {
        private ClearAnonymBsContainer _clearAnonymBsContainer;


        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string ConnectionString { get; set; }

        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string ContainerName { get; set; }

        [Parameter(
         Mandatory = false,
         HelpMessage = "The number of concurrent threas to copy files (Range 1.. 5000). Default is 512."
     )]
        [ValidateRange(1, 5000)]
        public int MaxParallelDownloads = 512;


        [Parameter(
            Mandatory = false,
            HelpMessage = "When is reuired to get info about each anonymized file via debug messages. Default is false. "
        )]
        public bool ShowEachFileName = false;


        protected override void BeginProcessing()
        {
            _clearAnonymBsContainer = new ClearAnonymBsContainer(ConnectionString, ContainerName);
        }

        protected override void ProcessRecord()
        {
            foreach (var blobName in _clearAnonymBsContainer.Clear(MaxParallelDownloads, ShowEachFileName))
            {
                WriteVerbose(blobName);
            }
        }
    }
}