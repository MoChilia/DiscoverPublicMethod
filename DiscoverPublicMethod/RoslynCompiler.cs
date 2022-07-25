using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using System.Xaml.Schema;
using System.Text.RegularExpressions;

namespace DiscoverPublicMethod
{
    public class RoslynCompiler
    {
        public static HashSet<SyntaxNode> visited = new HashSet<SyntaxNode>();
        public static List<List<string>> callChains = new List<List<string>>();
        public static Compilation compilation;
        public static IEnumerable<Project> projects;

        public async Task GetChainBottomUp(string solutionPath, string documentName)
        {
            var workspace = MSBuildWorkspace.Create();
            workspace.LoadMetadataForReferencedProjects = true;
            var solution = await workspace.OpenSolutionAsync(solutionPath);
            foreach (var project in solution.Projects)
            {
                compilation = await project.GetCompilationAsync();
                Console.WriteLine("Project Name is: " + project.Name);
                Console.WriteLine("Project Assembly Name is :" + project.AssemblyName);
                var document = project.Documents.Where(d => d.Name == documentName).FirstOrDefault();
                if (document == null)
                {
                    return;
                }
                Console.WriteLine("\tDocument Name is: " + document.Name);
                var assembly = new Asm();
                string assemblyName = "Microsoft.Azure.Management.KeyVault";
                List<Tuple<string, string>> methodNames = assembly.load(assemblyName);
                foreach (var method in methodNames)
                {
                    var declarationsToMethod = await SymbolFinder.FindDeclarationsAsync(project, method.Item2, false);
                    //if(!declarationsToMethod.Any())
                    //{
                    //    Console.WriteLine($"The method {method} is not called in {document.Name}");
                    //    continue;
                    //}
                    foreach(var declaration in declarationsToMethod)
                    {
                        if (declaration.ContainingType != null && method.Item1.Equals(declaration.ContainingType.ToString()))
                        {
                            var referencesToSampleMethod = await SymbolFinder.FindReferencesAsync(declaration, solution);
                            foreach (var reference in referencesToSampleMethod)
                            {
                                var referenceSymbol = reference.Definition;
                                if (!method.Item2.Equals(referenceSymbol.Name))
                                {
                                    Console.WriteLine($"The method {method.Item1}:{method.Item2} is called by {referenceSymbol.ContainingType}:{referenceSymbol.Name}");
                                }
                            }
                        }
                    }
                }
            }
        }

        public async Task GetChainTopDown(string solutionPath, string documentName)
        {
            var workspace = MSBuildWorkspace.Create();
            workspace.LoadMetadataForReferencedProjects = true;
            var solution = await workspace.OpenSolutionAsync(solutionPath);
            foreach (var project in solution.Projects)
            {
                compilation = await project.GetCompilationAsync();
                Console.WriteLine("Project Name is: " + project.Name);
                Console.WriteLine("Project Assembly Name is :" + project.AssemblyName);
                var document = project.Documents.Where(d => d.Name == documentName).FirstOrDefault();
                if (document != null)
                {
                    Console.WriteLine("\tDocument Name is: " + document.Name);
                    var model = await document.GetSemanticModelAsync();
                    var root = await document.GetSyntaxRootAsync();
                    var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
                    foreach (var method in methodDeclarations)
                    {
                        var currMethod = method;
                        List<string> callChain = new List<string>();
                        Console.WriteLine("\t\tMethod Name is: " + method.Identifier.Text);
                        currMethod = FindMethod(model, currMethod, callChain);
                    }
                    break;
                }
            }
        }
        public MethodDeclarationSyntax FindMethod(SemanticModel model, MethodDeclarationSyntax method, List<string> callChain)
        {
            if (method == null || model == null)
            {
                Console.WriteLine("The method is null now");
                return null;
            }
            if (visited.Contains(method))
            {
                Console.WriteLine("The method has visited");
                return null;
            }
            visited.Add(method);
            callChain.Add(method.Identifier.Text);
            var Invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (InvocationExpressionSyntax invoc in Invocations)
            {
                if (visited.Contains(invoc))
                {
                    Console.WriteLine("Have invoked the expression");
                    continue;
                }
                Console.WriteLine("\t\t\t\t\t-----Invocations-----");
                Console.WriteLine("\t\t\t\t\t" + invoc.Expression);  // output: b1.ADD
                var invokedSymbol = model.GetSymbolInfo(invoc).Symbol;
                if (invokedSymbol != null)
                {
                    Console.WriteLine("\t\t\t\t\t" + invokedSymbol.ToString()); //AppTest.B.ADD(int)
                    Console.WriteLine($"Method {method.Identifier.Text} invoke {invokedSymbol.Name}");
                    //Invoke API from third-party library
                    if (invokedSymbol.DeclaringSyntaxReferences.IsEmpty)
                    {
                        string symbolNameSpace = invokedSymbol.ContainingNamespace.ToString();
                        Console.WriteLine("\t\t\t\t\t" + symbolNameSpace);
                        if (symbolNameSpace.Contains("Microsoft.Azure.Management"))
                        {
                            List<string> callChainCopy = new List<string>();
                            callChain.ForEach(i => callChainCopy.Add(i));
                            callChainCopy.Add(invokedSymbol.ToString());
                            callChainCopy.Add("Invoke API from SDK");
                            callChains.Add(callChainCopy);
                            OutputCallChain(callChainCopy);
                        }
                        continue;
                    }
                    foreach (var reference in invokedSymbol.DeclaringSyntaxReferences)
                    {
                        var nextMethod = (MethodDeclarationSyntax)reference.GetSyntaxAsync().Result;
                        var tree = reference.SyntaxTree;
                        var nextModel = compilation.GetSemanticModel(tree);
                        //Console.WriteLine(nextMethod.Kind());

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