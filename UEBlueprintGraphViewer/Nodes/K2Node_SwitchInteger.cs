using CUE4Parse.UE4.Kismet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UEBlueprintGraphViewer.Decompiler;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_SwitchInteger : K2Node_Switch
    {
        public K2Node_SwitchInteger(GraphPin Selection, List<KeyValuePair<string, BlockJump?>> Cases, KismetExpression Instr) : base(Selection, Cases, Instr)
        {
            Name = "Switch on Int";
        }
    }
}
