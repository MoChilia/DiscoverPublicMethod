using System;
using System.Reflection;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.CSharp;
using System.CodeDom;
using System.Text;
using System.IO;
using System.CodeDom.Compiler;

namespace DiscoverPublicMethod
{
    public class Asm
    {
        public List<Tuple<string, string, string>> load(string asmName)
        {
            // Use the file name to load the assembly into the current application domain.
            var a = Assembly.Load(asmName);
            var typeNameList = new List<Tuple<string, string, string>>();
            foreach(var type in a.GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    string parameterName = "(";
                    int i = 1;
                    foreach (var parameter in method.GetParameters())
                    {
                        if(i < method.GetParameters().Count())
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
                    typeNameList.Add(new Tuple<string, string, string>(type.FullName, method.Name, parameterName));
                }
                //var interfaces = type.GetInterfaces();
            }
            return typeNameList;
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


