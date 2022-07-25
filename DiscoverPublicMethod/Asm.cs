using System;
using System.Reflection;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;

namespace DiscoverPublicMethod
{
    public class Asm
    {
        public List<Tuple<string, string>> load(string asmName)
        {
            // Use the file name to load the assembly into the current application domain.
            var a = Assembly.Load(asmName);
            var typeNameList = new List<Tuple<string, string>>();
            foreach(var type in a.GetTypes())
            {
                foreach(var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    typeNameList.Add(new Tuple<string, string>(type.FullName, method.Name));
                }
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
