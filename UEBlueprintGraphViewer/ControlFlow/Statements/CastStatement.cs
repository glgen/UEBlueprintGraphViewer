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
        private KismetExpression startInstr = startInstr;
        private EX_CastBase cast = cast;
        private EX_LocalVariable var = var;

        public override void Decompile()
        {
            // 1 - Let 'as object' local var to result of cast instruction
            K2Node_DynamicCast Node = MakeNode(cast, VarInstrToProperty(var, Context.Global));
            Context.AddNode(Node);

            Context.BlockIndex++;

            bool hasJump = false;
            // 2 - Let success local var (optional in pure nodes)
            if (Context.GetInstr() is EX_Let LetSuccess)
            {
                Context.MarkAsParsed();
                Context.BlockIndex++;
                
                // Success variable name
                string SuccessPropName = VarInstrToName(LetSuccess.Variable);
                
                // 3 - Jump if cast is failed (optional)
                if (Context.BlockIndex == Context.Block.Instructions.Count - 1)
                {
                    if (Context.Block.Type is BlockType.BranchEndIfNot or BlockType.JumpIfNot)
                    {
                        ParseJumpExpr(Context.GetInstr(), out KismetExpression? BoolExpr);

                        // Ensure that this jump related to cast
                        hasJump = BoolExpr is EX_VariableBase && VarInstrToName(BoolExpr) == SuccessPropName;
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
                    Node.AddOutputPin(successPin);
                    context.LocalVars.Create(SuccessPropName, successPin);
                }
            }
            
            // processing all branches
            if (hasJump)
            {
                Connect(Context.LastPin, Node.ExecPin);
                Context.ProcessBranch(Context.Block.Jumps[0], Node.ExecOutPin);
                if (Context.Block.Type is BlockType.JumpIfNot)
                {
                    Context.ProcessBranch(Context.Block.Jumps[1], Node.ExecFailedPin);
                }
            }
            else
            {
                // if the cast does not have a jump, this node is pure
                Node.Pure = true;
                Node.Output.Remove(Node.ExecFailedPin!);
                Node.Output.Remove(Node.ExecOutPin!);
                Node.Input.Remove(Node.ExecPin!);
                Node.ExecFailedPin = null;
                Node.ExecOutPin = null;
                Node.ExecPin = null;
                Context.ProcessBranch(Context.Block, Context.BlockIndex, Context.LastPin);
            }
        }

        private K2Node_DynamicCast MakeNode(EX_CastBase Cast, PropertyData Var)
        {
            GraphPin ObjectPin = Context.Decompiler.ArgToPin(Cast.Target);
            GraphPin AsObjectPin = new GraphPin("", EEdGraphPinDirection.EGPD_Output, MakePinType(PinType.Object));

            Context.LocalVars.Create(Var.Name, AsObjectPin);

            string ClassName = PackageIndexToName(Cast.ClassPtr);
            
            return Cast switch
            {
                EX_CrossInterfaceCast or // ??
                EX_ObjToInterfaceCast => new K2Node_DynamicCast(ObjectPin, AsObjectPin, ClassName, startInstr, true),
                EX_InterfaceToObjCast or // ??
                EX_DynamicCast => new K2Node_DynamicCast(ObjectPin, AsObjectPin, ClassName, startInstr),
                EX_MetaCast => new K2Node_ClassDynamicCast(ObjectPin, AsObjectPin, ClassName, startInstr),
                _ => throw new DecompilerException($"Unknown cast instruction type - {Cast.GetType()}", Context),
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
