using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CSHttpServer
{
    public class RuntimeCompiler : IDisposable
    {

        CSharpCodeProvider provider;
        CompilerResults results;
        const string TempFilePath = @"./temp";

        static readonly CompilerParameters compilerParam = new CompilerParameters()
        {            
            GenerateInMemory = true,
            GenerateExecutable = false,
            TempFiles = new TempFileCollection(TempFilePath, false)
        };

        public RuntimeCompiler()
        {            
            provider = new CSharpCodeProvider();
            compilerParam.ReferencedAssemblies.Add(
                Assembly.GetExecutingAssembly().Location);
        }

        public Assembly Compiling(FileInfo file)
        {
            if (!Directory.Exists(TempFilePath)) Directory.CreateDirectory(TempFilePath);
            results = provider.CompileAssemblyFromFile(compilerParam, file.FullName);
            CheckError(results);
            return results.CompiledAssembly;
        }

        public Assembly Compiling(string code)
        {
            if (!Directory.Exists(TempFilePath)) Directory.CreateDirectory(TempFilePath);
            results = provider.CompileAssemblyFromSource(compilerParam, code);
            CheckError(results);
            return results.CompiledAssembly;
        }

        private static void CheckError(CompilerResults results)
        {
            if (results.Errors.HasErrors)
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine(String.Format("\nFileName: {0}", results.Errors[0].FileName));
                foreach (CompilerError error in results.Errors)
                {
                    sb.AppendLine(String.Format("Error ({0}): {1}　Line: {2}", error.ErrorNumber, error.ErrorText, error.Line));
                }

                throw new InvalidOperationException(sb.ToString());
            }
        }

        public static MethodInfo GetMethod(Assembly assembly, string className, string methodName)
        {
            return assembly.GetType(className).GetMethod(methodName);
        }

        public void Dispose()
        {
            provider?.Dispose();
        }
    }
}
