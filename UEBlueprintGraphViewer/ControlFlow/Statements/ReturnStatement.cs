using System;
using System.Collections.Generic;
using System.Linq;
using UEBlueprintGraphViewer.Decompiler;
using UEBlueprintGraphViewer.Nodes;
using static UEBlueprintGraphViewer.Engine.Utils;

namespace UEBlueprintGraphViewer.ControlFlow.Statements
{
    public class ReturnStatement : ControlFlowStatement
    {
        public ReturnStatement(DecompilerContext context) : base(context) { }

        public override void Decompile()
        {
            Context.MarkAsParsed();

            List<GraphPin> ReturnPins = [.. Context.LocalVars.GetOutPins().Select(o => o.Clone())];

            // no need to create return node on events and without params
            if (!Context.Global.IsUbergraph && ReturnPins.Count > 0)
            {
                K2Node_FunctionResult ReturnNode = new K2Node_FunctionResult(ReturnPins, Context.GetInstr());
                Context.AddNode(ReturnNode);
                Connect(Context.LastPin, ReturnNode.ExecPin);
            }
        }
    }
}
