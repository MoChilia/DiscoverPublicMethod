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
            var solutionPath = @"D:\AzPwsh\azure-powershell\src\Compute\Compute.sln";
            var projectName = "Compute";
            string assemblyName = "Microsoft.Azure.Management.Compute";
            {
                var roslynCompiler = new RoslynCompiler();
                string filePath = $"D:\\AzPwsh\\DiscoverPublicMethod\\DiscoverPublicMethod\\{projectName}-{assemblyName}-BottomUp.txt";
                if (File.Exists(filePath)) { File.Delete(filePath); }
                File.Create(filePath);
                await roslynCompiler.GetChainBottomUp(solutionPath, projectName, assemblyName);
                roslynCompiler.OutputCallChains(true, true, filePath);
            }
            //{
            //    var roslynCompiler = new RoslynCompiler();
            //    string filePath = $"D:\\AzPwsh\\DiscoverPublicMethod\\DiscoverPublicMethod\\{projectName}-{assemblyName}-TopDown.txt";
            //    if (File.Exists(filePath)) { File.Delete(filePath); }
            //    File.Create(filePath);
            //    await roslynCompiler.GetChainTopDown(solutionPath, projectName, assemblyName);
            //    roslynCompiler.OutputCallChains(true, true, filePath);
            //}
        }
    }
}
