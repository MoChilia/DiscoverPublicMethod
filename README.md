# DiscoverPublicMethod

This project is used to find out through which paths the public methods in source code refer to the APIs in the SDK. A call chain is found to figue out the public methods call which methods, and these methods call which methods, recursively, until an API in SDK is called. When you execute the main function, you can get a dictionary recorded the public method and the called API's source type and version.

The project includes three csharp files, they are Program.cs, Asm.cs and RoslynCompiler.cs. The following is an introduction for them.

## Asm.cs

## LoadMethods

The function is used to load methods from assembly. Since we just focus on the SDK assembly, we use the project name to load the assembly “Microsoft.Azure.Management.\*”. First we aim to find Apis' resource type and version.  There is a internal static class called "**SdkInfo**" in the assembly and a public static property called "**ApiInfo_*ManagementClient**" in this class. This property records Apis'resource type and version.  Next, we are going to find all the public methods in this assembly . Note that we need to exclude the private classes, nested private classes and classes in “Microsoft.Azure.Management.Model.\*” namespace. 

## GetApiInfo

The function is used to get ApiInfo by method's type name. The class "\*ManagementClient" controls all the public. There may be an entry in  "ApiInfo_*ManagementClient" whose first item is "\*ManagementClient". When "\*ManagementClient" has a version, I think it is assumed that all the versions are the same. If there is no one, then ignore it. We won't call a method in this class. Maybe getting ApiInfo through string processing is not the most proper way, it can be improved.

## GetParameterName

The function is used to get parameters from **MethodInfo**. It invokes [GetPrimitiveNameOfParameterType](#GetPrimitiveNameOfParameterType) to get the primitive name of each type of the parameters so that we can compare them with the parameters output by **ISymbol.ToString()** returned by SymbolFinder.FindDeclarations. The output of this function ensures their formats are the same.

## GetPrimitiveNameOfParameterType

The function is used to get primitive name of parameter type. For example, **System.String** should be turned into **string**. Note that **System.Nullable`1[System.Int32]** is a special case, it should be turned into **int?**. 

## RoslynCompiler.cs

### GetChainBottomUp

The function is used to get call chains by bottom-up way. The start point of a call chain is a public method found in the assembly. This function invokes  [LoadMethods](#LoadMethods)  to load methods from the assembly. Then it tries to find the ISymbols of these methods through the method name by  **SymbolFinder.FindDeclarations**. We should judge whether the found method is actually the method we found in the assembly by comparing their namespace and parameters. Then it invokes [FindMethodUp](#FindMethodUp) to find which methods in the source codes call these found public methods recursively.  Finally, the destination of the call chain is a method who has not called by any other methods. 

###FindMethodUp

The function is used to find a call chain by visiting methods recursively. The input of this function is the ISymbol of a function, it is used to find references where the method are referred by **SymbolFinder.FindReferences**. If there is no reference and the call chain is empty, then the found ISymbol is not used in the source code, it should not be stored in the call chain. If the current call chain is not empty, then the ISymbol should be recorded in the call chain and the **methodsApiInfo** dictionary.  What's more, the **callChain** is used to record the visited ISymbols. If there is reference found, we need to judge whether the found ISymbol is already in the call chain to avoid a dead loop firstly. Then it invokes [GetParentDeclaration](#GetParentDeclaration) to find the parent of this found ISymbol (in the body of which function).  Finally, it invokes [FindMethodUp](#FindMethodUp) to find the upper method recursively.

###GetParentDeclaration

The function is used to find the parent of the ISymbol. To put it simply, it will return the body of which function the ISymbol is in. Since the ISymbol may occur in method, property, constructor, etc, we limit the type range of the parent to **MemberDeclarationSyntax** and **MethodDeclarationSyntax**.

###CompilationDiagnostics

The function is used to output diagnostics of compilation, when compilation fails.

###WorkspaceDiagnostics

The function is used to output diagnostics of workspace, when it failed to create a workspace. When errors like

*\VisualStudioIde\MSBuild\Current\Bin\amd64\Microsoft.Common.CurrentVersion.targets: (1806, 5): The "GetReferenceNearestTargetFrameworkTask" task could not be instantiated from the assembly "D:\VisualStudioIde\Common7\IDE\CommonExtensions\Microsoft\NuGet\NuGet.Build.Tasks.dll". Please verify the task assembly has been built using the same version of the Microsoft.Build.Framework assembly as the one installed on your computer and that your host application is not missing a binding redirect for Microsoft.Build.Framework.* 

happens, you should consider that whether the version of your msbuild is wrong.

###OutputMethodsApiInfo

The function is used to output the dictionary: MethodsApiInfo in the format that Function: {function} calls Api with 

SourceType: {apiInfo.SourceType}; ApiVersion: {apiInfo.ApiVersion}...

SourceType: {apiInfo.SourceType}; ApiVersion: {apiInfo.ApiVersion}

It has two switch parameter to control whether it needs to output in console or a file.

### OutputCallChain

The function is used for debug. It can output the detailed information of ISymbols in  call chain.

### RegisterInstance

The function is used to register a MSBuild instance with the MSBuildLocator. The reason why I use a MSBuildLocator to register a MSBuild instance is that the version of the local MSBuild and the version of the imported MSBuild are matched. I think if we run this project in a setting with no MSBuild, then we can import them instead of using a MSBuildLocator.

### SelectVisualStudioInstance

If there are more than one instances of MSBuild on this machine, use this function to set one to use. It may not be a good way to let users choose a version of MSBuild. 

##Program.cs

### Main

The main function invokes [GetChainBottomUp](#GetChainBottomUp) to get call chain by the bottom-up way and output the results to the file and console. 

## CallChainCache

This folder stores some call chain caches.

##Other notes

If you want to see the code of get call chain by the top-down way. It can be found in former commits [DiscoverPublicMethod/RoslynCompiler.cs at 56393ea5c748b805c47fb54bd1ffb584dd03dbee · MoChilia/DiscoverPublicMethod (github.com)](https://github.com/MoChilia/DiscoverPublicMethod/blob/56393ea5c748b805c47fb54bd1ffb584dd03dbee/DiscoverPublicMethod/RoslynCompiler.cs). See the function GetChainTopDown and FindMethodDown for more details. The two functions are discarded in this version because we choose the bottom-up way to get call chain.



