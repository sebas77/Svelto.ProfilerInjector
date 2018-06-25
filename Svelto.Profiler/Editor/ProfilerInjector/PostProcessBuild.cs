#if INJECT_PROFILER
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;
using UnityEditor;
#if AUTO_RUN_FOR_IL2CPP_BUILD
using UnityEditor.Callbacks;
#endif    
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using FileAttributes = System.IO.FileAttributes;
using MethodBody = Mono.Cecil.Cil.MethodBody;

public static class AssemblyPostProcessor
    {
        static bool done = false;
#if AUTO_RUN_FOR_IL2CPP_BUILD
        [PostProcessScene]
#endif        
        static void OnSceneProcess()
        {
            if (done == false && Application.isPlaying == false)
            {
                Debug.Log("<color=yellow>Start Scene Processing</color> ");
                InternalApp(string.Empty);
                done = true;
            }
        }
#if AUTO_RUN_FOR_IL2CPP_BUILD
        [PostProcessBuild]
#endif        
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            done = false;
        }

        [MenuItem("Tools/InjectAssembly")]
        public static void ProcessIT()
        {
            InternalApp(string.Empty);
        }
        
        public static void ProcessIT(string additionalFolder)
        {
            InternalApp(additionalFolder);
        }

        static void InternalApp(string additionalFolder)
        {
            Debug.Log("AssemblyPostProcessor running");

            // Lock assemblies while they may be altered
            EditorApplication.LockReloadAssemblies();

            // This will hold the paths to all the assemblies that will be processed
            var assemblyPaths = new HashSet<string>();
            // This will hold the search directories for the resolver
            var assemblySearchDirectories = new HashSet<string>();
            // Create resolver
            var assemblyResolver = new DefaultAssemblyResolver();

            if (additionalFolder == string.Empty)
            {
                // Add all assemblies in the project to be processed, and add their directory to
                // the resolver search directories.
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    // Only process assemblies which are in the project

                    string assemblyLocation = string.Empty;

                    try
                    {
                        assemblyLocation = assembly.Location;
                    }
                    catch (Exception e)
                    {
                        Debug.Log("<color=orange>" + assembly.FullName + " " + e.Message + "</color>");
                    }

                    if (assemblyLocation != string.Empty &&
                        assemblyLocation.Replace('\\', '/')
                                        .StartsWith(Application.dataPath.Substring(0, Application.dataPath.Length - 7)))
                    {
                        Debug.Log("Adding assembly for patching: " + assembly.Location);
                        assemblyPaths.Add(assembly.Location);
                    }

                    // But always add the assembly folder to the search directories
                    try
                    {
                        if (assemblyLocation != string.Empty)
                            assemblySearchDirectories.Add(Path.GetDirectoryName(assembly.Location));
                    }
                    catch (Exception e)
                    {
                        Debug.Log("Exception while fetching assembly: " + assembly.Location + ", " + e.Message);
                    }
                }
                
                // Add all directories found in the project folder
                foreach (string searchDirectory in assemblySearchDirectories)
                {
                    assemblyResolver.AddSearchDirectory(searchDirectory);
                }

                // Add path to the Unity managed dlls
                assemblyResolver.AddSearchDirectory(Path.GetDirectoryName(EditorApplication.applicationPath) +
                                                    "/Data/Managed");
            }
            else
            {
                string path = additionalFolder;

                foreach (string dll in Directory.GetFiles(path, "*.dll"))
                    assemblyPaths.Add(dll);

                assemblyResolver.AddSearchDirectory(path);
            }

            // Create reader parameters with resolver
            var readerParameters = new ReaderParameters();
            readerParameters.AssemblyResolver = assemblyResolver;

            // Create writer parameters
            var writerParameters = new WriterParameters();

            // Process any assemblies which need it
            foreach (string assemblyPath in assemblyPaths)
            {
                if (assemblyPath.Contains("Cecil") || assemblyPath.Contains("Editor"))
                {
                    Debug.Log("<color=yellow>Skipping:</color> " + assemblyPath);
                    continue;
                }

                // mdbs have the naming convention myDll.dll.mdb whereas pdbs have myDll.pdb
                string mdbPath = assemblyPath + ".mdb";
                string pdbPath = assemblyPath.Substring(0, assemblyPath.Length - 3) + "pdb";

                // Figure out if there's an pdb/mdb to go with it
                if (File.Exists(pdbPath))
                {
                    Debug.Log("<color=green>PDB found</color> " + assemblyPath + "...");
                    File.SetAttributes(pdbPath, FileAttributes.Normal);
                    readerParameters.ReadSymbols = true;
                    readerParameters.SymbolReaderProvider = new PdbReaderProvider();
                    writerParameters.WriteSymbols = true;
                    // pdb written out as mdb, as mono can't work with pdbs
                    writerParameters.SymbolWriterProvider = new MdbWriterProvider();
                }
                else 
                if (File.Exists(mdbPath))
                {
                    Debug.Log("<color=green>MDB found</color> " + assemblyPath + "...");
                    File.SetAttributes(mdbPath, FileAttributes.Normal);
                    readerParameters.ReadSymbols = true;
                    readerParameters.SymbolReaderProvider = new MdbReaderProvider();
                    writerParameters.WriteSymbols = true;
                    writerParameters.SymbolWriterProvider = new MdbWriterProvider();
                }
                else
                {
                    readerParameters.ReadSymbols = false;
                    readerParameters.SymbolReaderProvider = null;
                    writerParameters.WriteSymbols = false;
                    writerParameters.SymbolWriterProvider = null;
                }

                try
                {
                    // Process it if it hasn't already
                    Debug.Log("<color=green>Processing</color> " + assemblyPath + "...");
                    
                    // Read assembly
                    var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters);
                    
                    if (ProcessAssembly(assembly))
                    {
                        File.SetAttributes(assemblyPath, FileAttributes.Normal);
                        Debug.Log("<color=cyan>Writing to</color>  " + assemblyPath + "...");
                        assembly.Write(assemblyPath, writerParameters);
                    }
                    else
                    {
                        Debug.Log("<color=cyan>Skipping assembly</color>  " + assemblyPath + "...");
                    }
                }
                catch (Exception e)
                {
                    // Skip writing if any exception occurred
                    Debug.LogError("Exception while processing assembly: " + assemblyPath + ", " + e.Message);
                }
            }

            // Unlock now that we're done
            EditorApplication.UnlockReloadAssemblies();
        }
        
        static bool ProcessAssembly(AssemblyDefinition assembly)
        {
            var changed = false;

            var moduleG = assembly.MainModule;

            var attributeConstructor =
                    moduleG.ImportReference(
                        typeof(RamjetProfilerPostProcessedAssemblyAttribute).GetConstructor(Type.EmptyTypes));
            var attribute = new CustomAttribute(attributeConstructor);
            var ramjet = moduleG.ImportReference(typeof(RamjetProfilerPostProcessedAssemblyAttribute));
            if (assembly.HasCustomAttributes)
            {
                var attributes = assembly.CustomAttributes;
                foreach (var attr in attributes)
                {
                    if (attr.AttributeType.FullName == ramjet.FullName)
                    {
                        Debug.Log("<color=yellow>Skipping already-patched assembly:</color>  " + assembly.Name);
                        return false;
                    }
                }
            }
            assembly.CustomAttributes.Add(attribute);

            foreach (var module in assembly.Modules)
            {
                Debug.Log("Processing module: " + module.FullyQualifiedName);
                
                var beginMethod =
                    module.ImportReference(typeof(Profiler).GetMethod("BeginSample",
                                                                      BindingFlags.Static |
                                                                      BindingFlags.Public, null,                                                                       
                                                                      new[] { typeof(string) },null));
                var endMethod =
                    module.ImportReference(typeof(Profiler).GetMethod("EndSample",
                                                                      BindingFlags.Static |
                                                                      BindingFlags.Public));
                
                foreach (var type in module.Types)
                {
                    // Skip any classes related to the RamjetProfiler
                    if (type.Name.Contains("AssemblyPostProcessor") || type.Name.Contains("RamjetProfiler"))
                    {
                        // Todo: use actual type equals, not string matching
                        Debug.Log("<color=blue>Skipping self class :</color> " + type.Name);
                        continue;
                    }
                    
                    var s = "Profilator->" + type.FullName + ".";

                    if (type.BaseType != null && type.BaseType.FullName.Contains("UnityEngine.MonoBehaviour"))
                    {
                        foreach (var method in type.Methods)
                        {
                            if ((method.Name == "Update" || method.Name == "LateUpdate" || method.Name == "FixedUpdate") &&
                                method.HasParameters == false)
                            {
                                Debug.Log(method.Name + " method found in class: " + type.Name);

                                var ilProcessor = method.Body.GetILProcessor();

                                var first = method.Body.Instructions[0];
                                
                                ilProcessor.InsertBefore(first,
                                                         Instruction.Create(OpCodes.Ldstr,
                                                                            s + method.Name));
                                ilProcessor.InsertBefore(first, Instruction.Create(OpCodes.Call, beginMethod));

                                var lastcall = Instruction.Create(OpCodes.Call, endMethod);

                                FixReturns(method, lastcall);

                                changed = true;
                            }
                        }
                    }
                }
            }

            return changed;
        }

        static void FixReturns(MethodDefinition med, Instruction lastcall)
        {
            MethodBody body = med.Body;

            var instructions = body.Instructions;
            Instruction formallyLastInstruction = instructions[instructions.Count - 1];
            Instruction lastLeaveInstruction = null;
            
            var lastRet = Instruction.Create(OpCodes.Ret);
            instructions.Add(lastcall);
            instructions.Add(lastRet);

            for (var index = 0; index < instructions.Count - 1; index++)
            {
                var instruction = instructions[index];
                if (instruction.OpCode == OpCodes.Ret)
                {
                    Instruction leaveInstruction = Instruction.Create(OpCodes.Leave, lastcall);
                    if (instruction == formallyLastInstruction)
                    {
                        lastLeaveInstruction = leaveInstruction;
                    }

                    instructions[index] = leaveInstruction;
                }
            }

            FixBranchTargets(lastLeaveInstruction, formallyLastInstruction, body);
        }

         static void FixBranchTargets(
            Instruction lastLeaveInstruction,
            Instruction formallyLastRetInstruction,
            MethodBody body)
        {
            for (var index = 0; index < body.Instructions.Count - 2; index++)
            {
                var instruction = body.Instructions[index];
                if (instruction.Operand != null && instruction.Operand == formallyLastRetInstruction)
                {
                    instruction.Operand = lastLeaveInstruction;
                }
            }
        }
        
        
    }
#endif