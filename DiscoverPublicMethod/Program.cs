﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscoverPublicMethod
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var roslynCompiler = new RoslynCompiler();
            var solutionPath = @"D:\AzPwsh\azure-powershell\src\KeyVault\KeyVault.sln";
            var projectName = "KeyVault";
            string assemblyName = "Microsoft.Azure.Management.KeyVault";
            await roslynCompiler.GetChainBottomUp(solutionPath, projectName, assemblyName);
            roslynCompiler.OutputCallChains();

            //await roslynCompiler.GetChainTopDown(solutionPath, projectName);
            //roslynCompiler.OutputCallChains();
        }
    }
}
