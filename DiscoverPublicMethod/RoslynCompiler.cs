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
        //public static List<List<ISymbol>> callChains = new List<List<ISymbol>>();
        public static List<List<string>> callChains = new List<List<string>>();
        public static Compilation compilation;
        public static IEnumerable<Project> projects;
        public static Solution solution;

        public async Task GetChainBottomUp(string solutionPath, string projectName)
        {
            RegisterInstance();
            var workspace = MSBuildWorkspace.Create();
            workspace.LoadMetadataForReferencedProjects = true;
            solution = await workspace.OpenSolutionAsync(solutionPath);
            var project = solution.Projects.SingleOrDefault(p => p.Name == projectName);
            compilation = await project.GetCompilationAsync();
            var assembly = new Asm();
            string assemblyName = $"Microsoft.Azure.Management.{projectName}";
            Console.WriteLine("Project Name is: " + project.Name);
            Console.WriteLine("Assembly Name is: " + assemblyName);
            List<Tuple<string, string, string, string, string>> methodNames = assembly.loadMethods(projectName);
            foreach (var method in methodNames)
            {
                var methodFullName = method.Item1 + "." + method.Item2 + method.Item3;
                var externalFunctions = await SymbolFinder.FindDeclarationsAsync(project, method.Item2, true);
                foreach (var externalFunction in externalFunctions)
                {
                    if (methodFullName.Equals(externalFunction.ToString()))
                    {
                        //Console.WriteLine(externalFunction.ToString());
                        List<string> callChain = new List<string>();
                        List<SyntaxNode> callChainNode = new List<SyntaxNode>();
                        await FindMethodUp(externalFunction, callChain, callChainNode, method);
                    }
                }
            }
        }

        public async Task FindMethodUp(ISymbol function, List<string> callChain, List<SyntaxNode> callChainNode, Tuple<string, string, string, string, string> method)
        {
            var callers = await SymbolFinder.FindReferencesAsync(function, solution);
            var referenced = callers.FirstOrDefault();
            if (referenced.Locations.Count() == 0 && callChain.Any())
            {
                var line = function.Locations.FirstOrDefault().GetLineSpan().StartLinePosition.Line + 1;
                var filePath = $"[FileName (line#){function.Locations.FirstOrDefault().SourceTree.FilePath} ({line})]";
                callChain.Add($"{function.ToString()}{filePath}");
                List<string> callChainCopy = new List<string>();
                callChain.Reverse();
                callChain.ForEach(i => callChainCopy.Add(i));
                callChains.Add(callChainCopy);
            }
            foreach (var location in referenced.Locations)
            {
                var line = location.Location.GetLineSpan().StartLinePosition.Line + 1;
                var filePath = $"[FileName (line#){location.Location.SourceTree.FilePath} ({line})]";
                List<string> callChainMemory = new List<string>();
                callChain.ForEach(i => callChainMemory.Add(i));
                List<SyntaxNode> callChainNodeMemory = new List<SyntaxNode>();
                callChainNode.ForEach(i => callChainNodeMemory.Add(i));
                if (!callChain.Any())
                {
                    callChain.Add($"{function.ToString()}{filePath}[Api version: {method.Item5}]");
                }
                else
                {
                    callChain.Add($"{function.ToString()}{filePath}");
                }
                var document = location.Document;
                var root = await document.GetSyntaxRootAsync();
                var model = await document.GetSemanticModelAsync();
                var referencedNode = root.FindNode(location.Location.SourceSpan);
                var parentDeclaration = GetParentDeclaration(root, referencedNode);
                if (parentDeclaration == null)
                    continue;
                var nextSymbol = model.GetDeclaredSymbol(parentDeclaration);
                if (!SymbolEqualityComparer.Default.Equals(nextSymbol, function))
                {
                    await FindMethodUp(nextSymbol, callChain, callChainNode, method);
                }
                callChain.Clear();
                callChainMemory.ForEach(i => callChain.Add(i));
                callChainNode.Clear();
                callChainNodeMemory.ForEach(i => callChainNode.Add(i));
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


        public async Task GetChainTopDown(string solutionPath, string projectName, string assemblyName)
        {
            var workspace = MSBuildWorkspace.Create();
            workspace.LoadMetadataForReferencedProjects = true;
            var solution = await workspace.OpenSolutionAsync(solutionPath);
            var project = solution.Projects.SingleOrDefault(p => p.Name == projectName);
            compilation = await project.GetCompilationAsync();
            compilationDiagnostics(compilation);
            Console.WriteLine("Project Name is: " + project.Name);
            foreach (var document in project.Documents)
            {
                var model = await document.GetSemanticModelAsync();
                var root = await document.GetSyntaxRootAsync();
                var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
                foreach (var method in methodDeclarations)
                {
                    List<string> callChain = new List<string>();
                    List<SyntaxNode> callChainNode = new List<SyntaxNode>();
                    await FindMethodDown(model, method, callChain, assemblyName, callChainNode);
                }
            }
        }
        public async Task FindMethodDown(SemanticModel model, MethodDeclarationSyntax method, List<string> callChain, string assemblyName, List<SyntaxNode> callChainNode)
        {
            var loc = method.GetLocation();
            var line = loc.GetLineSpan().StartLinePosition.Line + 1;
            var filePath = $"[FileName (line#){loc.SourceTree.FilePath} ({line})]";
            callChainNode.Add(method);
            callChain.Add(method.Identifier.Text + filePath);
            var Invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (InvocationExpressionSyntax invoc in Invocations)
            {
                var invokedSymbol = model.GetSymbolInfo(invoc).Symbol;
                if (invokedSymbol != null)
                {
                    //Invoke API from third-party library
                    if (invokedSymbol.DeclaringSyntaxReferences.IsEmpty)
                    {
                        string symbolNameSpace = invokedSymbol.ContainingNamespace.ToString();
                        //if (symbolNameSpace.Contains("Microsoft.Azure.Management"))
                        if (symbolNameSpace.Equals(assemblyName))
                        {
                            List<string> callChainCopy = new List<string>();
                            callChain.ForEach(i => callChainCopy.Add(i));
                            loc = invoc.GetLocation();
                            line = loc.GetLineSpan().StartLinePosition.Line + 1;
                            filePath = $"[FileName (line#){loc.SourceTree.FilePath} ({line})]";
                            var dllPath = $"[{invokedSymbol.Locations.FirstOrDefault().ToString()}]";
                            callChainCopy.Add(invokedSymbol.ToString() + filePath + dllPath);
                            callChains.Add(callChainCopy);
                        }
                        continue;
                    }
                    foreach (var reference in invokedSymbol.DeclaringSyntaxReferences)
                    {
                        var nextNode = await reference.GetSyntaxAsync();
                        if (nextNode.IsKind(SyntaxKind.DelegateDeclaration))
                        {
                            continue;
                        }
                        var nextMethod = (MethodDeclarationSyntax)nextNode;
                        var tree = reference.SyntaxTree;
                        var nextModel = compilation.GetSemanticModel(tree);
                        var nextSymbol = nextModel.GetSymbolInfo(nextMethod).Symbol;
                        List<string> callChainMemory = new List<string>();
                        callChain.ForEach(i => callChainMemory.Add(i));
                        List<SyntaxNode> callChainNodeMemory = new List<SyntaxNode>();
                        callChainNode.ForEach(i => callChainNodeMemory.Add(i));
                        if (!callChainNode.Contains(nextMethod))
                        {
                            await FindMethodDown(nextModel, nextMethod, callChain, assemblyName, callChainNode);
                        }
                        callChain.Clear();
                        callChainMemory.ForEach(i => callChain.Add(i));
                        callChainNode.Clear();
                        callChainNodeMemory.ForEach(i => callChainNode.Add(i));
                    }
                }
            }
            return;
        }

        private void compilationDiagnostics(Compilation compilation)
        {
            if (compilation.GetDiagnostics().Any())
            {
                var errors = compilation.GetDiagnostics().ToList();
                foreach (var diag in errors)
                    Console.WriteLine(diag);
            }
        }
        private void workspaceDiagnostics(MSBuildWorkspace workspace)
        {
            ImmutableList<WorkspaceDiagnostic> diagnostics = workspace.Diagnostics;
            foreach (var diagnostic in diagnostics)
            {
                Console.WriteLine(diagnostic.Message);
            }
        }

        public void OutputCallChain(List<string> callChain, bool inConsole = true, bool inFile = false, string filePath = null)
        {
            if (inConsole)
            {
                Console.WriteLine("Here is a call chain:");
                int i = 0;
                foreach (var item in callChain)
                {
                    if (i == 0)
                    {
                        Console.WriteLine(item);
                    }
                    else
                    {
                        Console.WriteLine("->" + item);
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
                            sw.WriteLine(item);
                        }
                        else
                        {
                            sw.WriteLine("->" + item);
                        }
                        i++;
                    }
                    sw.WriteLine("\n");
                }
            }

        }
        public void OutputCallChains(bool inConsole = true, bool inFile = false, string filePath = null)
        {
            if (inFile && filePath != null)
            {
                if (File.Exists(filePath)) { File.Delete(filePath); }
                File.Create(filePath).Close();
            }
            foreach (var callChain in callChains)
            {
                OutputCallChain(callChain, inConsole, inFile, filePath);
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