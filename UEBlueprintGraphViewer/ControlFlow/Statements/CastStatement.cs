using CUE4Parse.UE4.Kismet;
using UEBlueprintGraphViewer.Nodes;
using static UEBlueprintGraphViewer.Engine.Utils;
using static UEBlueprintGraphViewer.Engine.PropertiesUtils;
using static UEBlueprintGraphViewer.Engine.EngineEnums;
using UEBlueprintGraphViewer.Engine;
using UEBlueprintGraphViewer.Decompiler;
using static UEBlueprintGraphViewer.Engine.EngineBPData;

namespace UEBlueprintGraphViewer.ControlFlow.Statements
{
    public class CastStatement(DecompilerContext context, KismetExpression startInstr, EX_CastBase cast, EX_LocalVariable var)
        : ControlFlowStatement(context)
    {
        private KismetExpression _startInstr = startInstr;
        private EX_CastBase _cast = cast;
        private EX_LocalVariable _var = var;

        public override void Decompile()
        {
            // 1 - Let 'as object' local var to result of cast instruction
            K2Node_DynamicCast node = MakeNode(_cast, VarInstrToProperty(_var, Context.Global));
            Context.AddNode(node);

            Context.BlockIndex++;

            bool hasJump = false;
            // 2 - Let success local var (optional in pure nodes)
            if (Context.GetInstr() is EX_Let letSuccess)
            {
                Context.MarkAsParsed();
                Context.BlockIndex++;
                
                // Success variable name
                string successPropName = VarInstrToName(letSuccess.Variable);
                
                // 3 - Jump if cast is failed (optional)
                if (Context.BlockIndex == Context.Block.Instructions.Count - 1)
                {
                    if (Context.Block.Type is BlockType.BranchEndIfNot or BlockType.JumpIfNot)
                    {
                        ParseJumpExpr(Context.GetInstr(), out KismetExpression? boolExpr);

                        // Ensure that this jump related to cast
                        hasJump = boolExpr is EX_VariableBase && VarInstrToName(boolExpr) == successPropName;
                        if (hasJump)
                        {
                            Context.MarkAsParsed();
                        }
                    }
                }
                
                // if the cast does not have a jump, expose success value and remove failed execution pin
                if (!hasJump)
                {
                    GraphPin successPin = new("Success", EEdGraphPinDirection.EGPD_Output,
                        MakePinType(PinType.Bool));
                    node.AddOutputPin(successPin);
                    context.LocalVars.Create(successPropName, successPin);
                }
            }
            
            // processing all branches
            if (hasJump)
            {
                Connect(Context.LastPin, node.ExecPin);
                Context.ProcessBranch(Context.Block.Jumps[0], node.ExecOutPin);
                if (Context.Block.Type is BlockType.JumpIfNot)
                {
                    Context.ProcessBranch(Context.Block.Jumps[1], node.ExecFailedPin);
                }
            }
            else
            {
                // if the cast does not have a jump, this node is pure
                node.Pure = true;
                node.Output.Remove(node.ExecFailedPin!);
                node.Output.Remove(node.ExecOutPin!);
                node.Input.Remove(node.ExecPin!);
                node.ExecFailedPin = null;
                node.ExecOutPin = null;
                node.ExecPin = null;
                Context.ProcessBranch(Context.Block, Context.BlockIndex, Context.LastPin);
            }
        }

        private K2Node_DynamicCast MakeNode(EX_CastBase cast, PropertyData var)
        {
            GraphPin objectPin = Context.Decompiler.ArgToPin(cast.Target);
            GraphPin asObjectPin = new GraphPin("", EEdGraphPinDirection.EGPD_Output, MakePinType(PinType.Object));

            Context.LocalVars.Create(var.Name, asObjectPin);

            string className = PackageIndexToName(cast.ClassPtr);
            
            return cast switch
            {
                EX_CrossInterfaceCast or // ??
                EX_ObjToInterfaceCast => new K2Node_DynamicCast(objectPin, asObjectPin, className, _startInstr, true),
                EX_InterfaceToObjCast or // ??
                EX_DynamicCast => new K2Node_DynamicCast(objectPin, asObjectPin, className, _startInstr),
                EX_MetaCast => new K2Node_ClassDynamicCast(objectPin, asObjectPin, className, _startInstr),
                _ => throw new DecompilerException($"Unknown cast instruction type - {cast.GetType()}", Context),
            };
        }
        
        public static bool CheckAndDecompile(KismetExpression expr, DecompilerContext context)
        {
            CastStatement? statement = expr switch
            {
                EX_Let { Assignment: EX_CastBase cast, Variable: EX_LocalVariable var } =>
                    new CastStatement(context, expr, cast, var),
                EX_LetBase { Assignment: EX_CastBase cast2, Variable: EX_LocalVariable var2 } =>
                    new CastStatement(context, expr, cast2, var2),
                _ => null
            };
            statement?.Decompile();
            return statement != null;
        }
    }
}
