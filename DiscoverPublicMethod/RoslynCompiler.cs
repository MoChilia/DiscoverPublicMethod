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
using System.Threading;


namespace DiscoverPublicMethod
{
    public class RoslynCompiler
    {
        //public static List<List<ISymbol>> callChains = new List<List<ISymbol>>();
        public static List<List<string>> callChains = new List<List<string>>();
        public static Compilation compilation;
        public static IEnumerable<Project> projects;
        public static Solution solution;

        public async Task GetChainBottomUp(string solutionPath, string projectName, string assemblyName)
        {
            var workspace = MSBuildWorkspace.Create();
            workspace.LoadMetadataForReferencedProjects = true;
            solution = await workspace.OpenSolutionAsync(solutionPath);
            var project = solution.Projects.SingleOrDefault(p => p.Name == projectName);
            compilation = await project.GetCompilationAsync();
            Console.WriteLine("Project Name is: " + project.Name);
            var assembly = new Asm();
            List<Tuple<string, string, string>> methodNames = assembly.load(assemblyName);
            //var myFunction = compilation.GetSymbolsWithName(s => s.StartsWith("ListBySubscriptionWithHttpMessagesAsync"), SymbolFilter.All).FirstOrDefault();
            //if (myFunction != null)
            //{
            //    Console.WriteLine("50");
            //}
            var myClass = await SymbolFinder.FindDeclarationsAsync(project, "IAvailabilitySetsOperations", true);
            if (myClass.Any())
            {
                Console.WriteLine("myClass: " + myClass.FirstOrDefault().ToString());
            }
            foreach (var method in methodNames)
            {
                var methodFullName = method.Item1 + "." + method.Item2 + method.Item3;
                //Console.WriteLine("method: " + methodFullName);
                //if (method.Item2.Equals("ToString"))
                //{
                //    Console.WriteLine(methodFullName);
                //    var externalFunctionstest = await SymbolFinder.FindDeclarationsAsync(project, method.Item2, true);
                //    if (externalFunctionstest.Any())
                //    {
                //        Console.WriteLine("yes");
                //        Console.WriteLine(externalFunctionstest.FirstOrDefault().ToString());
                //    }
                //}
                var externalFunctions = await SymbolFinder.FindDeclarationsAsync(project, method.Item2, true);
                foreach (var externalFunction in externalFunctions)
                {
                    //Console.WriteLine("Yes");
                    //if (externalFunction.ContainingType != null && method.Item1.Equals(externalFunction.ContainingType.ToString()))
                    if (methodFullName.Equals(externalFunction.ToString()))
                    {
                        //Console.WriteLine("function: " + externalFunction.ToString());
                        List<string> callChain = new List<string>();
                        List<SyntaxNode> callChainNode = new List<SyntaxNode>();
                        await FindMethodUp(externalFunction, callChain, callChainNode);
                    }
                }
            }
        }

        public async Task FindMethodUp(ISymbol function, List<string> callChain, List<SyntaxNode> callChainNode)
        {
            var callers = await SymbolFinder.FindReferencesAsync(function, solution);
            var referenced = callers.FirstOrDefault();
            if (referenced.Locations.Count() == 0)
            {
                if (callChain.Any())
                {
                    var line = function.Locations.FirstOrDefault().GetLineSpan().StartLinePosition.Line + 1;
                    var filePath = $"[FileName (line#){function.Locations.FirstOrDefault().SourceTree.FilePath} ({line})]";
                    //Console.WriteLine(function.ToString() + filePath);
                    callChain.Add(function.ToString() + filePath);
                    List<string> callChainCopy = new List<string>();
                    callChain.Reverse();
                    callChain.ForEach(i => callChainCopy.Add(i));
                    callChains.Add(callChainCopy);
                }
            }
            foreach (var location in referenced.Locations)
            {
                var line = location.Location.GetLineSpan().StartLinePosition.Line + 1;
                var filePath = $"[FileName (line#){location.Location.SourceTree.FilePath} ({line})]";
                List<string> callChainMemory = new List<string>();
                callChain.ForEach(i => callChainMemory.Add(i));
                List<SyntaxNode> callChainNodeMemory = new List<SyntaxNode>();
                callChainNode.ForEach(i => callChainNodeMemory.Add(i));
                Console.WriteLine(function.ToString() + filePath);
                callChain.Add(function.ToString() + filePath);
                var document = location.Document;
                var root = await document.GetSyntaxRootAsync();
                var model = await document.GetSemanticModelAsync();
                var referencedNode = root.FindNode(location.Location.SourceSpan);
                var parentDeclaration = GetParentDeclaration(root, referencedNode);
                var nextSymbol = model.GetDeclaredSymbol(parentDeclaration);
                if (!SymbolEqualityComparer.Default.Equals(nextSymbol, function))
                {
                    await FindMethodUp(nextSymbol, callChain, callChainNode);
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
            if (compilation.GetDiagnostics().Any())
            {
                var errors = compilation.GetDiagnostics().ToList();
                foreach (var diag in errors)
                    Console.WriteLine(diag);
            }
            Console.WriteLine("Project Name is: " + project.Name);
            foreach(var document in project.Documents) 
            {
                //if (document.Name.Equals("GetAzureAvailabilitySetCommand.cs"))
                //if (document.Name.Equals("GetAzureKeyVault.cs"))
                {
                    Console.WriteLine("Document Name is: " + document.Name);
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
        }
        public async Task FindMethodDown(SemanticModel model, MethodDeclarationSyntax method, List<string> callChain, string assemblyName, List<SyntaxNode> callChainNode)
        {
            //var symbol = model.GetDeclaredSymbol(method);
            //if(symbol != null)
                //Console.WriteLine("method:" + symbol.ToString() + "\n");
            //callChain.Add(symbol);
            var loc = method.GetLocation();
            var line = loc.GetLineSpan().StartLinePosition.Line + 1;
            var filePath = $"[FileName (line#){loc.SourceTree.FilePath} ({line})]";
            //Console.WriteLine(method.Identifier.Text + filePath);
            callChainNode.Add(method);
            callChain.Add(method.Identifier.Text + filePath);
            //Console.WriteLine(method.Identifier.Text + filePath);
            var Invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (InvocationExpressionSyntax invoc in Invocations)
            {
                var semanticModel = compilation.GetSemanticModel(invoc.SyntaxTree);
                //Console.WriteLine("\n"+invoc);
                var invokedSymbol = semanticModel.GetSymbolInfo(invoc).Symbol;
                //var ssymbol = semanticModel.GetSymbolInfo(invoc).CandidateReason.ToString();
                //if (ssymbol != null)
                //{
                //    Console.WriteLine(ssymbol);
                //}
                //var invokedSymbol = model.GetDeclaredSymbol(invoc);
                //if (invoc.Expression.Kind() == SyntaxKind.SimpleMemberAccessExpression)
                //{
                //    Console.WriteLine("SimpleMemberAccessExpression");
                //    var memberAccess = (MemberAccessExpressionSyntax)invoc.Expression;
                //    if (memberAccess.Name != null)
                //    {
                //        var methodSymbol = model.GetSymbolInfo(memberAccess.Name).Symbol as IMethodSymbol;
                //        if(methodSymbol != null)
                //            Console.WriteLine("Y:" + methodSymbol.ToString());
                //    }
                //}
                if (invokedSymbol != null)
                {
                    Console.WriteLine("Y:"+invokedSymbol.ToString());
                    //Invoke API from third-party library
                    if (invokedSymbol.DeclaringSyntaxReferences.IsEmpty)
                    {
                        string symbolNameSpace = invokedSymbol.ContainingNamespace.ToString();
                        //if (symbolNameSpace.Contains("Microsoft.Azure.Management"))
                        if(symbolNameSpace.Equals(assemblyName))
                        {
                            List<string> callChainCopy = new List<string>();
                            callChain.ForEach(i => callChainCopy.Add(i));
                            loc = invoc.GetLocation();
                            line = loc.GetLineSpan().StartLinePosition.Line + 1;
                            filePath = $"[FileName (line#){loc.SourceTree.FilePath} ({line})]";
                            var dllPath = $"[{invokedSymbol.Locations.FirstOrDefault().ToString()}]";
                            //Console.WriteLine(invokedSymbol.ToString() + filePath + dllPath);
                            callChainCopy.Add(invokedSymbol.ToString() + filePath + dllPath);
                            callChains.Add(callChainCopy);
                        }
                        continue;
                    }
                    foreach (var reference in invokedSymbol.DeclaringSyntaxReferences)
                    {
                        var nextNode =  await reference.GetSyntaxAsync();
                        if(nextNode.IsKind(SyntaxKind.DelegateDeclaration))
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
        //public void OutputCallChain(List<ISymbol> callChain, bool inConsole = true, bool inFile = false, string filePath = null)
        //{
        //    if (inConsole)
        //    {
        //        Console.WriteLine("Here is a call chain:");
        //        int i = 0;
        //        foreach (var item in callChain)
        //        {
        //            var loc = item.Locations.FirstOrDefault();
        //            var line = loc.GetLineSpan().StartLinePosition.Line + 1;
        //            var fileName = $"[FileName (line#){loc.SourceTree.FilePath} ({line})]";
        //            if (i == 0)
        //            {
        //                Console.WriteLine(item.ToString()+fileName);
        //            }
        //            else
        //            {
        //                Console.WriteLine("->" + item.ToString() + fileName);
        //            }
        //            i++;
        //        }
        //        Console.WriteLine("\n");
        //    }
        //    if (inFile && filePath != null)
        //    {
        //        using (StreamWriter sw = new StreamWriter(filePath, append: true))
        //        {
        //            sw.WriteLine("Here is a call chain:");
        //            int i = 0;
        //            foreach (var item in callChain)
        //            {
        //                var loc = item.Locations.FirstOrDefault();
        //                var line = loc.GetLineSpan().StartLinePosition.Line + 1;
        //                var fileName = $"[FileName (line#){loc.SourceTree.FilePath} ({line})]";
        //                if (i == 0)
        //                {
        //                    sw.WriteLine(item.ToString() + fileName);
        //                }
        //                else
        //                {
        //                    sw.WriteLine("->" + item.ToString() + fileName);
        //                }
        //                i++;
        //            }
        //            sw.WriteLine("\n");
        //        }
        //    }
        //}
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
                    //sw.Flush();
                    //sw.Close();
                }
            }

        }
        public void OutputCallChains(bool inConsole = true, bool inFile = false, string filePath = null)
        {
            foreach(var callChain in callChains)
            {
                OutputCallChain(callChain, inConsole, inFile, filePath);
                //Thread.Sleep(500);
            }
        }
    }
}