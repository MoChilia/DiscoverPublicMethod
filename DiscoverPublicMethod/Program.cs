using System;
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
            var documentName = "GetAzureKeyVault.cs";
            await roslynCompiler.GetChainBottomUp(solutionPath, documentName);

            //roslynCompiler.GetChainTopDown(solutionPath, documentName);
            //roslynCompiler.OutputCallChains();

            //var assembly = new Asm();
            //string assemblyName = "Microsoft.Azure.Management.KeyVault";
            //assembly.load(assemblyName);
        }
    }
}
