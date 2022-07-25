using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DiscoverPublicMethod
{
    public class FindIdentifier
    {
        public void ExecuteSyntaxTree()
        {
            const string filePath = "D:\\AzPwsh\\azure-powershell\\src\\KeyVault\\KeyVault\\Commands\\GetAzureKeyVault.cs";
            string programText = System.IO.File.ReadAllText(@filePath);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(programText);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            Console.WriteLine($"The tree is a {root.Kind()} node.");
            Console.WriteLine($"The tree has {root.Members.Count} elements in it. They are:");
            foreach (MemberDeclarationSyntax element in root.Members)
                Console.WriteLine($"\t{element.Kind()}");
            Console.WriteLine($"The tree has {root.Usings.Count} using statements. They are:");
            foreach (UsingDirectiveSyntax element in root.Usings)
                Console.WriteLine($"\t{element.Name}");

            MemberDeclarationSyntax firstMember = root.Members[0];
            var NamespaceDeclaration = (NamespaceDeclarationSyntax)firstMember;
            Console.WriteLine($"There are {NamespaceDeclaration.Members.Count} members declared in this namespace.They are:");
            foreach (MemberDeclarationSyntax element in NamespaceDeclaration.Members)
                Console.WriteLine($"\t{element.Kind()}");

            var classDeclaration = (ClassDeclarationSyntax)NamespaceDeclaration.Members[0];
            Console.WriteLine($"There are {classDeclaration.Members.Count} members declared in the {classDeclaration.Identifier} class.");
            foreach (MemberDeclarationSyntax element in classDeclaration.Members)
                Console.WriteLine($"\t{element.Kind()}");

            var MethodDeclaration = (MethodDeclarationSyntax)classDeclaration.Members.Last();
            Console.WriteLine($"The return type of the {MethodDeclaration.Identifier} method is {MethodDeclaration.ReturnType}.");
            Console.WriteLine($"The method has {MethodDeclaration.ParameterList.Parameters.Count} parameters.");
            foreach (ParameterSyntax item in MethodDeclaration.ParameterList.Parameters)
                Console.WriteLine($"The type of the {item.Identifier} parameter is {item.Type}.");

            var Block = (BlockSyntax)MethodDeclaration.Body;
            //Console.WriteLine($"The body text of the {MethodDeclaration.Identifier} method follows:");
            //Console.WriteLine(Block.ToFullString());
            var SwitchStatement = (SwitchStatementSyntax)Block.Statements[0];
            foreach (SwitchSectionSyntax section in SwitchStatement.Sections)
            {
                Console.WriteLine($"Statements in Switch Section:");
                foreach (StatementSyntax statement in section.Statements)
                {
                    Console.WriteLine($"\t{statement.Kind()}");
                }
            }
            foreach (var invocation in Block.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                Console.WriteLine($"In InvocationExpression:");
                foreach (var identifier in invocation.DescendantNodes().OfType<IdentifierNameSyntax>())
                {
                    if (identifier.Parent.Kind() != SyntaxKind.Argument)
                    {
                        Console.WriteLine($"\t {identifier.Identifier}");
                        //Console.WriteLine($"\t Kind is: {identifier.Parent.Kind()}");
                    }

                }
            }
        }
    }
}
