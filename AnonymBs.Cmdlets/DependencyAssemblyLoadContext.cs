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
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace AnonymBs.Cmdlets
{
    public class DependencyAssemblyLoadContext : AssemblyLoadContext
    {
        private static readonly string s_psHome = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

        private static readonly ConcurrentDictionary<string, DependencyAssemblyLoadContext> s_dependencyLoadContexts = new ConcurrentDictionary<string, DependencyAssemblyLoadContext>();

        internal static DependencyAssemblyLoadContext GetForDirectory(string directoryPath)
        {
            return s_dependencyLoadContexts.GetOrAdd(directoryPath, (path) => new DependencyAssemblyLoadContext(path));
        }

        private readonly string _dependencyDirPath;

        public DependencyAssemblyLoadContext(string dependencyDirPath)
            : base(nameof(DependencyAssemblyLoadContext))
        {
            _dependencyDirPath = dependencyDirPath;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            string assemblyFileName = $"{assemblyName.Name}.dll";

            // Make sure we allow other common PowerShell dependencies to be loaded by PowerShell
            // But specifically exclude Azure.Storage.Blobs since we want to use a different version here
            if (!assemblyName.Name.Equals("Azure.Storage.Blobs", StringComparison.OrdinalIgnoreCase))
            {
                string psHomeAsmPath = Path.Join(s_psHome, assemblyFileName);
                if (File.Exists(psHomeAsmPath))
                {
                    // With this API, returning null means nothing is loaded
                    return null;
                }
            }

            // Now try to load the assembly from the dependency directory
            string dependencyAsmPath = Path.Join(_dependencyDirPath, assemblyFileName);
            if (File.Exists(dependencyAsmPath))
            {
                return LoadFromAssemblyPath(dependencyAsmPath);
            }

            return null;
        }
    }
}