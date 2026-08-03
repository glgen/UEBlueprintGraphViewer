using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;
using System.Linq;
using UEBlueprintGraphViewer.Decompiler;
using UEBlueprintGraphViewer.Engine;
using UEBlueprintGraphViewer.Nodes;
using static UEBlueprintGraphViewer.Engine.EngineBPData;
using static UEBlueprintGraphViewer.Engine.PropertiesUtils;
using static UEBlueprintGraphViewer.Engine.Utils;

namespace UEBlueprintGraphViewer.ControlFlow.Statements
{
    public class SpawnStatement : ControlFlowStatement
    {
        SpawnNodeType Type;

        string SpawnReturn = "";

        BPNode SpawnNode;
        GraphPin ReturnPin;

        public SpawnStatement(SpawnNodeType type, DecompilerContext context) : base(context)
        {
            Type = type;
        }

        // Processing spawn node instructions
        // Structure:
        // Begin spawn function call
        // Some instructions setting exposed on spawn variables mixed with something else
        // Make finish spawn transform (actor only)
        // Finish spawn call (actor only)
        public override void Decompile()
        {
            EX_LetObj spawnInstr = (Context.GetInstr() as EX_LetObj)!;

            // get spawn function parameters
            EX_FinalFunction spawnCall = GetSpawnCall(spawnInstr);
            var func = Context.Decompiler.FindFunctionInAsset(spawnCall.StackNode.ResolvedObject.Outer.Load(), spawnCall.StackNode.ResolvedObject.Outer.GetPathName(), spawnCall.StackNode.Name);
            List<GraphPin> Params = Context.Decompiler.ParseArgs(spawnCall.Parameters, func.Params);

            ReturnPin = Params.Last();
            SpawnReturn = VarInstrToName(spawnInstr.Variable);

            Context.BlockIndex++;
            Context.LocalVars.Create(SpawnReturn, ReturnPin);

            // add node
            SpawnNode = Type switch
            {
                SpawnNodeType.Actor => new K2Node_SpawnActorFromClass(Params, spawnInstr),
                SpawnNodeType.Obj => new K2Node_GenericCreateObject(Params, spawnInstr),
                SpawnNodeType.Widget => new K2Node_CreateWidget(Params, spawnInstr),
                _ => throw new DecompilerException($"Unknown spawn node type {Type}", Context),
            };
            Context.AddNode(SpawnNode);
            Connect(Context.LastPin, SpawnNode.ExecPin);
            Context.LastPin = SpawnNode.ExecOutPin!;
        }

        public bool IsThisObject(string name)
        {
            return SpawnReturn == name;
        }

        private void AddExposedParam(DecompilerContext context, EX_FinalFunction call)
        {
            context.MarkAsParsed();
            SpawnNode.AddInputPin(ExposedOnSpawnSetterToPin(call));
            context.BlockIndex++;
        }

        private void FinishSpawnActor(DecompilerContext context, EX_LetObj letObj)
        {
            context.MarkAsParsed();
            context.LocalVars.Create(VarInstrToName(letObj.Variable), ReturnPin);
            context.BlockIndex++;
        }

        private EX_FinalFunction GetSpawnCall(EX_LetObj spawnInstr)
        {
            if (Type == SpawnNodeType.Widget)
                return ((spawnInstr.Assignment as EX_Context)!.ContextExpression as EX_FinalFunction)!;
            return (spawnInstr.Assignment as EX_CallMath)!;
        }

        private GraphPin ExposedOnSpawnSetterToPin(EX_FinalFunction setter)
        {
            KismetExpression[] parms = setter.Parameters;
            string exposedVarName = (parms[1] as EX_NameConst)!.Value.ToString();

            return Context.Decompiler.ArgToPin(parms[2], exposedVarName);
        }
        
        public static bool CheckAndDecompile(KismetExpression expr, DecompilerContext context)
        {
            if (expr is not EX_LetObj letObj) return false;
            var type = GetSpawnNodeType(letObj);
            if (type == SpawnNodeType.None) return false;
            
            SpawnStatement spawnStatement = new SpawnStatement(type, context);
            context.ControlFlow.SpawnNodes.Add(spawnStatement);
            spawnStatement.Decompile();
            return true;
        }

        private static SpawnNodeType GetSpawnNodeType(EX_LetObj expr)
        {
            if (expr.Assignment is EX_CallMath callMath)
            {
                (string name, string outer) = callMath.GetNameAndOuter();
                if (outer != ActorAndObjectSpawnFunctionOuter) return SpawnNodeType.None;

                return name switch
                {
                    ActorSpawnFunctionName => SpawnNodeType.Actor,
                    ObjectSpawnFunctionName => SpawnNodeType.Obj,
                    _ => SpawnNodeType.None,
                };
            }
            
            if (expr.Assignment is EX_Context context)
            {
                bool isWidgetSpawn = IsFinalFunc(context.ContextExpression, WidgetSpawnFunctionName, WidgetSpawnFunctionOuter);
                return isWidgetSpawn ? SpawnNodeType.Widget : SpawnNodeType.None;
            }

            return SpawnNodeType.None;
        }

        public static bool IsSpawnActorEndFunc(KismetExpression expr, DecompilerContext context)
        {
            if (expr is EX_LetObj letObj && IsCallMathFunc(letObj.Assignment, "FinishSpawningActor", "GameplayStatics"))
            {
                string objectVar = VarInstrToName((letObj.Assignment as EX_CallMath)!.Parameters[0]);
                SpawnStatement? spawn = context.ControlFlow.FindSpawnStatement(objectVar);
                spawn?.FinishSpawnActor(context, letObj);
                return spawn != null;
            }
            return false;
        }

        public static bool IsExposedOnSpawnSetter(KismetExpression expr, DecompilerContext context)
        {
            if (expr is EX_CallMath call &&
                call.Parameters.Length == 3 &&
                call.Parameters[0] is EX_VariableBase)
            {
                (string FuncName, string OuterName) = call.GetNameAndOuter();
                if (OuterName == "KismetSystemLibrary" && FuncName.Starts("Set") && FuncName.Ends("PropertyByName"))
                {
                    return AddExposedPin(context, call);
                }
            }
            else if (expr is EX_Context con &&
                con.ContextExpression is EX_FinalFunction finalCall &&
                finalCall.Parameters.Length == 3 &&
                finalCall.Parameters[0] is EX_VariableBase)
            {
                (string FuncName, string OuterName) = finalCall.GetNameAndOuter();
                if ((OuterName == "KismetArrayLibrary" && FuncName == "SetArrayPropertyByName") ||
                    (OuterName == "BlueprintSetLibrary" && FuncName == "SetSetPropertyByName") ||
                    (OuterName == "BlueprintMapLibrary" && FuncName == "SetMapPropertyByName"))
                {
                    return AddExposedPin(context, finalCall);
                }
            }
            return false;
        }

        private static bool AddExposedPin(DecompilerContext context, EX_FinalFunction call)
        {
            string objectVar = VarInstrToName(call.Parameters[0]);
            SpawnStatement? spawn = context.ControlFlow.FindSpawnStatement(objectVar);
            spawn?.AddExposedParam(context, call);
            return spawn != null;
        }
    }
}
