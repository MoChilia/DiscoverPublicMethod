using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using System.Threading.Tasks;


namespace DiscoverPublicMethod
{
    public class RoslynCompiler
    {
        public static HashSet<SyntaxNode> visited = new HashSet<SyntaxNode>();
        public static List<List<string>> callChains = new List<List<string>>();
        public static Compilation compilation;
        public static IEnumerable<Project> projects;

        public async Task GetChainBottomUp(string solutionPath, string projectName)
        {
            var workspace = MSBuildWorkspace.Create();
            workspace.LoadMetadataForReferencedProjects = true;
            var solution = await workspace.OpenSolutionAsync(solutionPath);
            var project = solution.Projects.SingleOrDefault(p => p.Name == projectName);
            Console.WriteLine("Project Name is: " + project.Name);
            var assembly = new Asm();
            string assemblyName = "Microsoft.Azure.Management.KeyVault";
            List<Tuple<string, string,string>> methodNames = assembly.load(assemblyName);
            foreach (var method in methodNames)
            {
                var externalFunctions = await SymbolFinder.FindDeclarationsAsync(project, method.Item2, true);
                foreach (var externalFunction in externalFunctions)
                {
                    if (externalFunction.ContainingType != null && method.Item1.Equals(externalFunction.ContainingType.ToString()))
                    {
                        var callers = await SymbolFinder.FindReferencesAsync(externalFunction, solution);
                        foreach (var referenced in callers)
                        {
                            if(referenced.Locations.Count() == 0)
                            {
                                break;
                            }
                            Console.WriteLine($"Number of references {referenced.Definition.Name} {referenced.Locations.Count()}");
                            Console.WriteLine($"Function name is {externalFunction.ToString()}");
                            Console.WriteLine($"Method name is: {method.Item1}.{method.Item2}{method.Item3}");
                            foreach (var location in referenced.Locations)
                            {
                                Console.WriteLine($"FileName (line#) {location.Location.SourceTree.FilePath} ({location.Location.GetLineSpan().StartLinePosition.Line + 1})");
                            }
                            Console.WriteLine("\n");
                        }
                    }
                }
            }
        }

        public async Task GetChainTopDown(string solutionPath, string projectName)
        {
            var workspace = MSBuildWorkspace.Create();
            workspace.LoadMetadataForReferencedProjects = true;
            var solution = await workspace.OpenSolutionAsync(solutionPath);
            var project = solution.Projects.SingleOrDefault(p => p.Name == projectName);
            compilation = await project.GetCompilationAsync();
            Console.WriteLine("Project Name is: " + project.Name);
            foreach(var document in project.Documents) 
            {
                var model = await document.GetSemanticModelAsync();
                var root = await document.GetSyntaxRootAsync();
                var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
                foreach (var method in methodDeclarations)
                {
                    var currMethod = method;
                    List<string> callChain = new List<string>();
                    currMethod = FindMethod(model, currMethod, callChain);
                }
            }
        }
        public MethodDeclarationSyntax FindMethod(SemanticModel model, MethodDeclarationSyntax method, List<string> callChain)
        {
            if (method == null || model == null|| visited.Contains(method))
            {
                return null;
            }
            visited.Add(method);
            var loc = method.GetLocation();
            var line = loc.GetLineSpan().StartLinePosition.Line + 1;
            var filePath = $"[FileName (line#){loc.SourceTree.FilePath} ({line})]";
            callChain.Add(method.Identifier.Text + filePath);
            var Invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (InvocationExpressionSyntax invoc in Invocations)
            {
                if (visited.Contains(invoc))
                {
                    continue;
                }
                var invokedSymbol = model.GetSymbolInfo(invoc).Symbol;
                if (invokedSymbol != null)
                {
                    //Invoke API from third-party library
                    if (invokedSymbol.DeclaringSyntaxReferences.IsEmpty)
                    {
                        string symbolNameSpace = invokedSymbol.ContainingNamespace.ToString();
                        if (symbolNameSpace.Contains("Microsoft.Azure.Management"))
                        {
                            List<string> callChainCopy = new List<string>();
                            callChain.ForEach(i => callChainCopy.Add(i));
                            loc = invoc.GetLocation();
                            line = loc.GetLineSpan().StartLinePosition.Line + 1;
                            filePath = $"[FileName (line#){loc.SourceTree.FilePath} ({line})]";
                            var dllPath = $"[{invokedSymbol.Locations.FirstOrDefault().ToString()}]";
                            callChainCopy.Add(invokedSymbol.ToString()+ filePath + dllPath);
                            callChains.Add(callChainCopy);
                        }
                        continue;
                    }
                    foreach (var reference in invokedSymbol.DeclaringSyntaxReferences)
                    {
                        var nextMethod = (MethodDeclarationSyntax)reference.GetSyntaxAsync().Result;
                        var tree = reference.SyntaxTree;
                        var nextModel = compilation.GetSemanticModel(tree);

                        List<string> callChainMemory = new List<string>();
                        callChain.ForEach(i => callChainMemory.Add(i));

                        FindMethod(nextModel, nextMethod, callChain);

                        callChain.Clear();
                        callChainMemory.ForEach(i => callChain.Add(i));
                    }
                }
            }
            return null;
        }
        public void OutputCallChain(List<string> callChain)
        {
            Console.WriteLine("Here is a call chain:");
            int i = 0;
            foreach (var item in callChain)
            {
                if(i != callChain.Count - 1)
                {
                    Console.Write(item + "->");
                }
                else
                {
                    Console.Write(item);
                }
                i++;
            }
            Console.WriteLine("\n");
        }
        public void OutputCallChains()
        {
            foreach(var callChain in callChains)
            {
                OutputCallChain(callChain);
            }
        }
    }
}