using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using NUnit.Framework;
using MethodBody = Mono.Cecil.Cil.MethodBody;

namespace CecilRewriter
{
    public class Rewriter
    {
        [Test]
        public void RewriteAssembly()
        {
            var assemblyPath = @"..\..\..\CecilTest\bin\Debug\CecilTest.exe";
            using (var assemblyDef = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters
            {
                ReadSymbols = true,
                ReadWrite = true
            }))
            {
                //execute weaving
                DoWeave(assemblyDef.MainModule);

                //write back the results
                assemblyDef.Write(new WriterParameters
                {
                    WriteSymbols = true,
                });
            }
        }

        private void DoWeave(ModuleDefinition module)
        {
            var type = module.GetAllTypes().FirstOrDefault(it => it.Name.Equals("Program"));
            var method = type.GetMethods().FirstOrDefault(it => it.Name.Equals("Main"));

            method.Body.SimplifyMacros();

            PrintInfo(method);

            var instrs = CreateInstructionsToInject(module, method.Body);

            int idx = 0;
            var calls = method.Body.Instructions.Where(it => it.OpCode.Equals(OpCodes.Call)).ToArray();
            idx = method.Body.Instructions.IndexOf(calls[1]) + 1;

            //After the injection point all debugs are messed up. The lines before the injection point are OK.

            foreach (var instr in instrs.AsEnumerable().Reverse())
            {
                method.Body.Instructions.Insert(idx, instr);
            }

            PrintInfo(method);

            method.Body.InitLocals = true;

            method.Body.OptimizeMacros();

            PrintInfo(method);
        }

        protected void PrintInfo(MethodDefinition method)
        {
            Debug.Print("-------");
            var debugInfo = method.DebugInformation;
            foreach (var instruction in method.Body.Instructions)
            {
                Debug.Print($"{instruction.Offset} {instruction.ToString()}");
            }
            foreach (var sp in debugInfo.SequencePoints)
            {
                Debug.Print($"{sp.Offset} {sp.StartLine}:{sp.EndLine}[{sp.StartColumn}:{sp.EndColumn}] {sp.IsHidden} {sp.Document.Url} {BitConverter.ToString(sp.Document.Hash)}");
            }
        }


        private List<Instruction> CreateInstructionsToInject(ModuleDefinition moduleDefinition, MethodBody body)
        {
            var mRef2 = moduleDefinition.ImportReference(typeof(Console).GetMethod("WriteLine", BindingFlags.Public | BindingFlags.Static, null, new Type[]
            {
                typeof(string), typeof(object)
            }, null));

            var instructions = new List<Instruction>();

            //build up call
            instructions.Add(Instruction.Create(OpCodes.Ldstr, "Hello {0}!"));
            instructions.Add(Instruction.Create(OpCodes.Ldstr, "Joe"));
            instructions.Add(Instruction.Create(OpCodes.Call, mRef2));
            instructions.Add(Instruction.Create(OpCodes.Nop));

            //instructions.Add(Instruction.Create(OpCodes.Ldstr, "Hello {0}!"));
            //instructions.Add(Instruction.Create(OpCodes.Ldstr, "Jim"));
            //instructions.Add(Instruction.Create(OpCodes.Call, mRef2));

            //instructions.Add(Instruction.Create(OpCodes.Ldstr, "Bye {0}!"));
            //instructions.Add(Instruction.Create(OpCodes.Ldstr, "Jim"));
            //instructions.Add(Instruction.Create(OpCodes.Call, mRef2));

            return instructions;
        }
    }
}
