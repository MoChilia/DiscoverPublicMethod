using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using System.Threading.Tasks;
using System.Collections.Immutable;
using Microsoft.Build.Locator;


namespace DiscoverPublicMethod
{
    public class RoslynCompiler
    {
        public static Dictionary<ISymbol, HashSet<AzureApiInfo>> methodsApiInfo = new Dictionary<ISymbol, HashSet<AzureApiInfo>>();
        public static Solution solution;

        public async Task GetChainBottomUp(string solutionPath, string projectName)
        {
            RegisterInstance();
            var workspace = MSBuildWorkspace.Create();
            workspace.LoadMetadataForReferencedProjects = true;
            solution = await workspace.OpenSolutionAsync(solutionPath);
            var project = solution.Projects.SingleOrDefault(p => p.Name == projectName);
            Console.WriteLine("Project Name is: " + project.Name);
            Compilation compilation = await project.GetCompilationAsync();
            string assemblyName = $"Microsoft.Azure.Management.{projectName}";
            Console.WriteLine("Assembly Name is: " + assemblyName);
            var assembly = new Asm();
            List<AzureMethodInfo> methodInfos = assembly.LoadMethods(projectName);
            foreach (var method in methodInfos)
            {
                var methodFullName = method.NameSpace + "." + method.Method + method.Parameters;
                var externalFunctions = await SymbolFinder.FindDeclarationsAsync(project, method.Method, true);
                foreach (var externalFunction in externalFunctions)
                {
                    if (methodFullName.Equals(externalFunction.ToString()))
                    {
                        List<ISymbol> callChain = new List<ISymbol>();
                        await FindMethodUp(externalFunction, callChain, method);
                    }
                }
            }
        }

        public async Task FindMethodUp(ISymbol function, List<ISymbol> callChain, AzureMethodInfo method)
        {
            var callers = await SymbolFinder.FindReferencesAsync(function, solution);
            var referenced = callers.FirstOrDefault();
            // Reach the destination of the call chain which is a method not called by any other methods
            if (referenced.Locations.Count() == 0 && callChain.Any())
            {
                if (!methodsApiInfo.ContainsKey(function))
                {
                    HashSet<AzureApiInfo> apiInfoSet = new HashSet<AzureApiInfo>();
                    apiInfoSet.Add(method.ApiInfo);
                    methodsApiInfo.Add(function, apiInfoSet);
                }
                else
                {
                    HashSet<AzureApiInfo> apiInfoSet = methodsApiInfo[function];
                    if (!apiInfoSet.Contains(method.ApiInfo))
                    {
                        apiInfoSet.Add(method.ApiInfo);
                        methodsApiInfo[function] = apiInfoSet;
                    }
                }
                return;
            }
            List<ISymbol> callChainMemory = new List<ISymbol>();
            callChain.ForEach(i => callChainMemory.Add(i));
            callChain.Add(function);
            foreach (var location in referenced.Locations)
            {
                var document = location.Document;
                var root = await document.GetSyntaxRootAsync();
                var model = await document.GetSemanticModelAsync();
                var referencedNode = root.FindNode(location.Location.SourceSpan);
                var parentDeclaration = GetParentDeclaration(root, referencedNode);
                if (parentDeclaration == null)
                    continue;
                var nextSymbol = model.GetDeclaredSymbol(parentDeclaration);
                if (!callChain.Contains(nextSymbol))
                {
                    await FindMethodUp(nextSymbol, callChain, method);
                }
                callChain.Clear();
                callChainMemory.ForEach(i => callChain.Add(i));
            }
        }
        private static CSharpSyntaxNode GetParentDeclaration(SyntaxNode root, SyntaxNode referencedNode)
        {
            IEnumerable<CSharpSyntaxNode> candidates = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            if (!candidates.Any())
            {
                candidates = root.DescendantNodes().OfType<MemberDeclarationSyntax>();
            }
            var results = candidates.Where(candidate => candidate.DescendantNodesAndSelf().Contains(referencedNode));
            if (!results.Any())
            {
                return null;
            }
            var result = candidates.First(candidate => candidate.DescendantNodesAndSelf().Contains(referencedNode));
            return result;
        }

        private void CompilationDiagnostics(Compilation compilation)
        {
            if (compilation.GetDiagnostics().Any())
            {
                var diagnostics = compilation.GetDiagnostics().ToList();
                foreach (var diagnostic in diagnostics)
                    Console.WriteLine(diagnostic);
            }
        }
        private void WorkspaceDiagnostics(MSBuildWorkspace workspace)
        {
            ImmutableList<WorkspaceDiagnostic> diagnostics = workspace.Diagnostics;
            foreach (var diagnostic in diagnostics)
            {
                Console.WriteLine(diagnostic.Message);
            }
        }

        public void OutputMethodsApiInfo(bool inConsole = true, bool inFile = false, string filePath = null)
        {
            if (inFile && filePath != null)
            {
                if (File.Exists(filePath)) { File.Delete(filePath); }
                File.Create(filePath).Close();
            }
            foreach (var methodApiInfo in methodsApiInfo)
            {
                var function = methodApiInfo.Key;
                var apiInfoSet = methodApiInfo.Value;
                if (inConsole)
                {
                    Console.WriteLine($"Function: {function} calls Api with");
                    foreach (var apiInfo in apiInfoSet)
                    {
                        Console.WriteLine($"ResourceType: {apiInfo.ResourceType}; ApiVersion: {apiInfo.ApiVersion}");
                    }
                }
                if (inFile && filePath != null)
                {
                    using (StreamWriter sw = new StreamWriter(filePath, append: true))
                    {
                        sw.WriteLine($"Function: {function} calls Api with");
                        foreach (var apiInfo in apiInfoSet)
                        {
                            sw.WriteLine($"ResourceType: {apiInfo.ResourceType}; ApiVersion: {apiInfo.ApiVersion}");
                        }
                    }
                }
            }
        }

        public void OutputCallChain(List<ISymbol> callChain, bool inConsole = true, bool inFile = false, string filePath = null)
        {
            if (inConsole)
            {
                Console.WriteLine("Here is a call chain:");
                int i = 0;
                foreach (var item in callChain)
                {
                    if (i == 0)
                    {
                        Console.WriteLine(item.ToString());
                    }
                    else
                    {
                        Console.WriteLine("->" + item.ToString());
                    }
                    i++;
                }
                Console.WriteLine("\n");
            }
            if (inFile && filePath != null)
            {
                using (StreamWriter sw = new StreamWriter(filePath, append: true))
                {
                    sw.WriteLine("Here is a call chain:");
                    int i = 0;
                    foreach (var item in callChain)
                    {
                        if (i == 0)
                        {
                            sw.WriteLine(item.ToString());
                        }
                        else
                        {
                            sw.WriteLine("->" + item.ToString());
                        }
                        i++;
                    }
                    sw.WriteLine("\n");
                }
            }
        }

        private void RegisterInstance()
        {
            var visualStudioInstances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
            var instance = visualStudioInstances.Length == 1
                // If there is only one instance of MSBuild on this machine, set that as the one to use.
                ? visualStudioInstances[0]
                // Handle selecting the version of MSBuild you want to use.
                : SelectVisualStudioInstance(visualStudioInstances);

            Console.WriteLine($"Using MSBuild at '{instance.MSBuildPath}' to load projects.");

                // NOTE: Be sure to register an instance with the MSBuildLocator 
                //       before calling MSBuildWorkspace.Create()
                //       otherwise, MSBuildWorkspace won't MEF compose.
                MSBuildLocator.RegisterInstance(instance);
         }

        private static VisualStudioInstance SelectVisualStudioInstance(VisualStudioInstance[] visualStudioInstances)
        {
            Console.WriteLine("Multiple installs of MSBuild detected please select one:");
            for (int i = 0; i < visualStudioInstances.Length; i++)
            {
                Console.WriteLine($"Instance {i + 1}");
                Console.WriteLine($"    Name: {visualStudioInstances[i].Name}");
                Console.WriteLine($"    Version: {visualStudioInstances[i].Version}");
                Console.WriteLine($"    MSBuild Path: {visualStudioInstances[i].MSBuildPath}");
            }

            while (true)
            {
                var userResponse = Console.ReadLine();
                if (int.TryParse(userResponse, out int instanceNumber) &&
                    instanceNumber > 0 &&
                    instanceNumber <= visualStudioInstances.Length)
                {
                    return visualStudioInstances[instanceNumber - 1];
                }
                Console.WriteLine("Input not accepted, try again.");
            }
        }
    }
}