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
using System.IO;
using System.Management.Automation;
using System.Reflection;

#if NETFRAMEWORK
using System;
#else
using System.Runtime.Loader;
#endif

namespace AnonymBs.Cmdlets
{
    public class PsModuleInitializer : IModuleAssemblyInitializer
    {
        private static string s_binBasePath = Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                ".."));

        private static string s_binCommonPath = Path.Combine(s_binBasePath, "Common");

#if NETFRAMEWORK
        private static string s_binFrameworkPath = Path.Combine(s_binBasePath, "Framework");
#else
        private static string s_binCorePath = Path.Join(s_binBasePath, "Core");
#endif

        public void OnImport()
        {
#if NETFRAMEWORK
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly_NetFramework;
#else
            AssemblyLoadContext.Default.Resolving += ResolveAssembly_NetCore;
#endif
        }

#if NETFRAMEWORK

        private static Assembly ResolveAssembly_NetFramework(object sender, ResolveEventArgs args)
        {
            // In .NET Framework, we must try to resolve ALL assemblies under the dependency dir here
            // This essentially means we are combining the .NET Core ALC and resolve events into one here
            // Note that:
            //   - This is not a recommended usage of Assembly.LoadFile()
            //   - Even doing this will not bypass the GAC

            // Parse the assembly name to get the file name
            var asmName = new AssemblyName(args.Name);
            var dllFileName = $"{asmName.Name}.dll";

            // Look for the DLL in our .NET Framework directory
            string frameworkAsmPath = Path.Combine(s_binFrameworkPath, dllFileName);
            if (File.Exists(frameworkAsmPath))
            {
                return LoadAssemblyFile_NetFramework(frameworkAsmPath);
            }

            // Now look in the dependencies directory to resolve .NET Standard dependencies
            string commonAsmPath = Path.Combine(s_binCommonPath, dllFileName);
            if (File.Exists(commonAsmPath))
            {
                return LoadAssemblyFile_NetFramework(commonAsmPath);
            }

            // We've run out of places to look
            return null;
        }

        private static Assembly LoadAssemblyFile_NetFramework(string assemblyPath)
        {
            return Assembly.LoadFile(assemblyPath);
        }

#else

        private static Assembly ResolveAssembly_NetCore(
            AssemblyLoadContext assemblyLoadContext,
            AssemblyName assemblyName)
        {
            // In .NET Core, PowerShell deals with assembly probing so our logic is much simpler
            // We only care about our Engine assembly
            if (!assemblyName.Name.Equals("AnonymBs.Engine"))
            {
                return null;
            }

            // Now load the Engine assembly through the dependency ALC, and let it resolve further dependencies automatically
            return DependencyAssemblyLoadContext.GetForDirectory(s_binCommonPath).LoadFromAssemblyName(assemblyName);
        }

#endif
    }
}