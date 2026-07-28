using CUE4Parse.UE4.Kismet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UEBlueprintGraphViewer.Decompiler;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_SwitchEnum : K2Node_Switch
    {
        public K2Node_SwitchEnum(GraphPin Selection, List<KeyValuePair<string, BlockJump?>> Cases, EnumData? enumData, KismetExpression Instr) : base(Selection, Cases, Instr)
        {
            Name = $"Switch on {(enumData == null ? "Enum" : enumData.Name)}";
        }
    }
}
