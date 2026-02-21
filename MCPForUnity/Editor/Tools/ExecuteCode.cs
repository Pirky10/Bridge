using System;
using System.Reflection;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Text;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("execute_code", AutoRegister = false)]
    public static class ExecuteCode
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);
            var actionResult = p.GetRequired("action");
            var actionError = actionResult.GetOrError(out string action);
            if (actionError != null) return actionError;

            action = action.ToLowerInvariant();

            try
            {
                switch (action)
                {
                    case "run":
                        return Run(@params, p);
                    case "evaluate":
                        return Evaluate(@params, p);
                    case "run_static_method":
                        return RunStaticMethod(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: run, evaluate, run_static_method");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static object Run(JObject @params, ToolParams p)
        {
            var codeResult = p.GetRequired("code");
            var codeError = codeResult.GetOrError(out string code);
            if (codeError != null) return codeError;

            // Wrap in a class if needed
            string fullCode;
            if (code.Contains("class ") || code.Contains("namespace "))
            {
                fullCode = code;
            }
            else
            {
                fullCode = $@"
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class DynamicCode
{{
    public static object Execute()
    {{
        {code}
        return ""Executed successfully"";
    }}
}}";
            }

            // Compile
            var provider = new CSharpCodeProvider();
            var compilerParams = new CompilerParameters
            {
                GenerateInMemory = true,
                GenerateExecutable = false
            };

            // Add common Unity assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (!string.IsNullOrEmpty(assembly.Location))
                        compilerParams.ReferencedAssemblies.Add(assembly.Location);
                }
                catch { }
            }

            CompilerResults results = provider.CompileAssemblyFromSource(compilerParams, fullCode);

            if (results.Errors.HasErrors)
            {
                var errors = new List<string>();
                foreach (CompilerError error in results.Errors)
                {
                    if (!error.IsWarning)
                        errors.Add($"Line {error.Line}: {error.ErrorText}");
                }
                return new ErrorResponse("Compilation failed", new { errors });
            }

            // Execute
            try
            {
                Type dynamicType = results.CompiledAssembly.GetType("DynamicCode");
                if (dynamicType == null)
                {
                    // Try to find any static class with an Execute method
                    foreach (var type in results.CompiledAssembly.GetTypes())
                    {
                        var method = type.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static);
                        if (method != null)
                        {
                            dynamicType = type;
                            break;
                        }
                    }
                }

                if (dynamicType == null)
                    return new ErrorResponse("No DynamicCode class or Execute method found.");

                MethodInfo executeMethod = dynamicType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static);
                if (executeMethod == null)
                    return new ErrorResponse("No static Execute() method found.");

                object result = executeMethod.Invoke(null, null);

                return new SuccessResponse("Code executed", new
                {
                    result = result?.ToString(),
                    resultType = result?.GetType().Name
                });
            }
            catch (TargetInvocationException tie)
            {
                return new ErrorResponse($"Runtime error: {tie.InnerException?.Message}", new
                {
                    stackTrace = tie.InnerException?.StackTrace
                });
            }
        }

        private static object Evaluate(JObject @params, ToolParams p)
        {
            var expressionResult = p.GetRequired("expression");
            var exprError = expressionResult.GetOrError(out string expression);
            if (exprError != null) return exprError;

            // Wrap expression in return statement
            string code = $@"
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class DynamicEval
{{
    public static object Execute()
    {{
        return ({expression});
    }}
}}";

            var provider = new CSharpCodeProvider();
            var compilerParams = new CompilerParameters
            {
                GenerateInMemory = true,
                GenerateExecutable = false
            };

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (!string.IsNullOrEmpty(assembly.Location))
                        compilerParams.ReferencedAssemblies.Add(assembly.Location);
                }
                catch { }
            }

            CompilerResults results = provider.CompileAssemblyFromSource(compilerParams, code);

            if (results.Errors.HasErrors)
            {
                var errors = new List<string>();
                foreach (CompilerError error in results.Errors)
                {
                    if (!error.IsWarning)
                        errors.Add($"Line {error.Line}: {error.ErrorText}");
                }
                return new ErrorResponse("Compilation failed", new { errors });
            }

            try
            {
                Type evalType = results.CompiledAssembly.GetType("DynamicEval");
                MethodInfo executeMethod = evalType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static);
                object result = executeMethod.Invoke(null, null);

                // Try to serialize complex objects
                string resultStr;
                if (result == null)
                    resultStr = "null";
                else if (result is string || result.GetType().IsPrimitive)
                    resultStr = result.ToString();
                else
                    resultStr = JsonUtility.ToJson(result, true);

                return new SuccessResponse("Expression evaluated", new
                {
                    result = resultStr,
                    resultType = result?.GetType().FullName
                });
            }
            catch (TargetInvocationException tie)
            {
                return new ErrorResponse($"Runtime error: {tie.InnerException?.Message}", new
                {
                    stackTrace = tie.InnerException?.StackTrace
                });
            }
        }

        private static object RunStaticMethod(JObject @params, ToolParams p)
        {
            var typeNameResult = p.GetRequired("type_name");
            var typeError = typeNameResult.GetOrError(out string typeName);
            if (typeError != null) return typeError;

            var methodNameResult = p.GetRequired("method_name");
            var methodError = methodNameResult.GetOrError(out string methodName);
            if (methodError != null) return methodError;

            // Find the type
            Type targetType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                targetType = assembly.GetType(typeName);
                if (targetType != null) break;
            }

            if (targetType == null)
                return new ErrorResponse($"Type '{typeName}' not found.");

            MethodInfo method = targetType.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
                return new ErrorResponse($"Static method '{methodName}' not found on '{typeName}'.");

            // Parse arguments
            JToken argsToken = p.GetRaw("arguments");
            object[] args = null;

            if (argsToken != null && argsToken.Type == JTokenType.Array)
            {
                var argsArray = argsToken as JArray;
                var paramInfos = method.GetParameters();
                args = new object[argsArray.Count];

                for (int i = 0; i < argsArray.Count && i < paramInfos.Length; i++)
                {
                    try
                    {
                        args[i] = argsArray[i].ToObject(paramInfos[i].ParameterType);
                    }
                    catch
                    {
                        args[i] = Convert.ChangeType(argsArray[i].ToString(), paramInfos[i].ParameterType);
                    }
                }
            }

            try
            {
                object result = method.Invoke(null, args);

                return new SuccessResponse($"Executed {typeName}.{methodName}", new
                {
                    result = result?.ToString(),
                    resultType = result?.GetType().Name
                });
            }
            catch (TargetInvocationException tie)
            {
                return new ErrorResponse($"Runtime error: {tie.InnerException?.Message}", new
                {
                    stackTrace = tie.InnerException?.StackTrace
                });
            }
        }
    }
}
