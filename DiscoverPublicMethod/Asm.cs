using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CSharp;

namespace DiscoverPublicMethod
{
    public struct myApiInfo
    {
        public myApiInfo(string sourceType, string apiVersion)
        {
            SourceType = sourceType;
            ApiVersion = apiVersion;
        }
        public string SourceType { get; set; }
        public string ApiVersion { get; set; }
        public override string ToString() => $"({SourceType}, {ApiVersion})";
    }
    public struct myMethodInfo
    {
        public myMethodInfo(string nameSpace, string method, string parameters, string resourceType, string apiVersion)
        {
            NameSpace = nameSpace;
            Method = method;
            Parameters = parameters;
            ApiInfo = new myApiInfo(resourceType, apiVersion);
        }
        public string NameSpace { get; set; }
        public string Method { get; set; }
        public string Parameters { get; set; }
        public myApiInfo ApiInfo { get; set; }
        public override string ToString() => $"{NameSpace}.{Method}.{Parameters}[SourceType: {ApiInfo.SourceType}, ApiVersion: {ApiInfo.ApiVersion}]";
    }

    public class Asm
    {
        public List<myMethodInfo> loadMethods(string projectName)
        {
            // Use the projrect name to load the SDK assembly into the current application domain.
            string asmName = $"Microsoft.Azure.Management.{projectName}";
            var a = Assembly.Load(asmName);
            var SdkInfo = a.GetType($"{asmName}.SdkInfo");
            var ApiInfos = (IEnumerable<Tuple<string, string, string>>) SdkInfo.GetProperty($"ApiInfo_{projectName}ManagementClient").GetValue(null);
            var typeNameList = new List<myMethodInfo>();
            foreach(var type in a.GetTypes())
            {
                if (type.IsNestedPrivate || type.IsNotPublic || !type.Namespace.Equals(asmName))
                {
                    continue;
                }
                var ApiInfo = GetApiInfo(type.Name, ApiInfos);
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
                {
                    string parameterName = GetParameterName(method);
                    typeNameList.Add(new myMethodInfo(type.FullName, method.Name, parameterName, ApiInfo.SourceType, ApiInfo.ApiVersion));
                }
            }
            return typeNameList;
        }

        private myApiInfo GetApiInfo(string typeName, IEnumerable<Tuple<string, string, string>> ApiInfos)
        {
            string sourceType = "null";
            string apiVersion = "null";
            Tuple<string, string, string> source;
            if (typeName.Contains("ManagementClient"))
            {
                source = ApiInfos.Where(i => i.Item1.Contains("ManagementClient")).FirstOrDefault();
            }
            else
            {
                if (typeName.EndsWith("Extensions"))
                {
                    typeName = typeName.Substring(0, typeName.Length - 10);
                }
                source = ApiInfos.Where(i => i.Item2 == typeName).FirstOrDefault();
                if (source == null && typeName.EndsWith("Operations"))
                {
                    if (!typeName.Equals("IOperations"))
                    {
                        typeName = typeName.Substring(0, typeName.Length - 10);
                        source = ApiInfos.Where(i => i.Item2 == typeName).FirstOrDefault();
                    }
                    if (source == null && typeName.StartsWith("I"))
                    {
                         typeName = typeName.Substring(1);
                        source = ApiInfos.Where(i => i.Item2 == typeName).FirstOrDefault();
                    }
                }
            }
            if (source != null)
            {
                sourceType = source.Item1 + "." + source.Item2;
                apiVersion = source.Item3;
            }
            myApiInfo sourceVersionPair = new myApiInfo(sourceType, apiVersion);
            return sourceVersionPair;
        }

        private string GetParameterName(MethodInfo method)
        {
            string parameterName = "(";
            int i = 1;
            foreach (var parameter in method.GetParameters())
            {
                if (i < method.GetParameters().Count())
                {
                    parameterName = parameterName + GetPrimitiveNameOfParameterType(parameter.ParameterType) + ", ";
                }
                else
                {
                    parameterName = parameterName + GetPrimitiveNameOfParameterType(parameter.ParameterType);
                }
                i++;
            }
            parameterName += ")";
            return parameterName;
        }


        private string GetPrimitiveNameOfParameterType(Type parameterType)
        {
            string primitiveName;
            var compiler = new CSharpCodeProvider();
            // Nullable needs special treatment to turn System.Nullable`1[System.Int32] into int?
            if (Nullable.GetUnderlyingType(parameterType) != null)
            {
                primitiveName = compiler.GetTypeOutput(new System.CodeDom.CodeTypeReference(Nullable.GetUnderlyingType(parameterType))) + "?";
            }
            else
            {
                primitiveName = compiler.GetTypeOutput(new System.CodeDom.CodeTypeReference(parameterType));
            }
            return primitiveName;
        }
    }
}


