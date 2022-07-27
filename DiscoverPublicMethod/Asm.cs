using System;
using System.Reflection;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;

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
                //Console.WriteLine("Type is: "+ type.FullName);
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    string parameterName = "(";
                    //Console.WriteLine("method is: " + method.Name);
                    int i = 1;
                    foreach (var parameter in method.GetParameters())
                    {
                        if(i < method.GetParameters().Count())
                        {
                            parameterName = parameterName + parameter.ParameterType + ", ";
                        }
                        else
                        {
                            parameterName = parameterName + parameter.ParameterType;
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

        public void trace()
        {
            StackTrace trace = new StackTrace(true);
            foreach (StackFrame frame in trace.GetFrames())
            {
                Console.WriteLine("GetFileColumnNumber:" + frame.GetFileColumnNumber());
                Console.WriteLine("GetFileLineNumber:" + frame.GetFileLineNumber());
                Console.WriteLine("GetFileName:" + frame.GetFileName());
                Console.WriteLine("GetType:" + frame.GetType());
                Console.WriteLine("GetILOffset:" + frame.GetILOffset());
                Console.WriteLine("GetNativeOffset:" + frame.GetNativeOffset());
                Console.WriteLine(frame.GetMethod().Name);
            }
        }
    }
}
