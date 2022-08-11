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
            var projectName = "KeyVault";
            var solutionPath = $"D:\\AzPwsh\\azure-powershell\\src\\{projectName}\\{projectName}.sln";
            string assemblyName = $"Microsoft.Azure.Management.{projectName}";
            var roslynCompiler = new RoslynCompiler();
            await roslynCompiler.GetChainBottomUp(solutionPath, projectName);
            string filePath = $"D:\\AzPwsh\\DiscoverPublicMethod\\DiscoverPublicMethod\\CallChainCache\\{projectName}-{assemblyName}.txt";
            roslynCompiler.OutputMethodsApiInfo(true, true, filePath);
        }
    }
}
