using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CUE4Parse.UE4.Assets.Exports;
using UEBlueprintGraphViewer.Assets;
using UEBlueprintGraphViewer.ControlFlow;
using UEBlueprintGraphViewer.ControlFlow.Statements;
using UEBlueprintGraphViewer.Engine;
using UEBlueprintGraphViewer.Nodes;
using static UEBlueprintGraphViewer.BPGraph;
using static UEBlueprintGraphViewer.ControlFlow.ControlFlowUtils;
using static UEBlueprintGraphViewer.Engine.EngineBPData;
using static UEBlueprintGraphViewer.Engine.EngineEnums;
using static UEBlueprintGraphViewer.Engine.PropertiesUtils;
using static UEBlueprintGraphViewer.Engine.Utils;

namespace UEBlueprintGraphViewer.Decompiler
{
    public class FunctionDecompiler
    {
        readonly public GlobalDecompilerContext GlobalContext;

        readonly public BPGraph Graph = new();

        public LocalVariablesStorage LocalVars;

        UpdateProgressDelegate? _updateProgress;

        DecompilationResult _result;

        public FunctionDecompiler(GlobalDecompilerContext globalContext)
        {
            GlobalContext = globalContext;
        }
        
        public FunctionDecompiler(Asset asset, GameSettings game, UFunction func)
        {
            GlobalContext = new(asset, game, func);
        }

        public Task<DecompilationResult> DecompileAsync(UpdateProgressDelegate? updateProgress)
        {
            return Task.Run(() => Decompile(updateProgress));
        }

        // Main decompile function
        public DecompilationResult Decompile(UpdateProgressDelegate? updateProgress)
        {
            _updateProgress = updateProgress;

            _result = new();

            try
            {
                if (GlobalContext.IsUbergraph)
                {
                    DecompileUbergraph();
                }
                else
                {
                    LocalVariablesStorage localVars = ParseFunctionArguments(GlobalContext.FunctionToDecompile);
                    BPNode entryNode = new K2Node_FunctionEntry(GlobalContext.FunctionToDecompile.Name, localVars.GetLocalPins(), null);
                    Graph.AddNode(entryNode);
                    InitTempVariables(localVars);
                    ControlFlowContext flowContext = new();
                    flowContext.Flow.DecompileFlow(GlobalContext.CurrentFunction.ScriptBytecode);
                    StartDecompilation(localVars, entryNode.ExecOutPin!, flowContext);
                }
            }
            catch (DecompilerException e)
            {
                _result.AddProblem(e.Message, e.Context, true);
            }
            catch (Exception e)
            {
                _result.AddProblem($"{e.Message}\n{e.StackTrace}", null, true);
            }

            // remove connections to phantom pins
            foreach (BPNode node in Graph.Nodes)
            {
                node.RemoveFakeConnections();
            }

            CheckUnreachedPoints();
            
            if (GlobalContext.IsParsingMacros)
                Graph.ProcessMacros();
            
            // remove temp variables if only used once
            List<BPNode> toRemove = [];
            foreach (var node in Graph.Nodes.OfType<K2Node_TemporaryVariable>())
            {
                var assignments = node.VarPin.LinkedTo.Select(o => o.ParentNode).OfType<K2Node_AssignmentStatement>().ToArray();
                // check if this variable is used only once and have only one static value in this graph
                // 0 static - default value, 1 static - constant value
                if (assignments.Length <= 1 && node.VarPin.LinkedTo.Count - assignments.Length == 1)
                {
                    toRemove.Add(node);
                    if (assignments.Length == 1)
                    {
                        assignments[0].ExecPin!.LinkedTo[0].Disconnect(assignments[0].ExecPin!);
                        if (assignments[0].ExecOutPin!.LinkedTo.FirstOrDefault() != null)
                        {
                            assignments[0].ExecOutPin!.LinkedTo[0].Disconnect(assignments[0].ExecOutPin!);
                            Connect(assignments[0].ExecPin!.LinkedTo[0], assignments[0].ExecOutPin!.LinkedTo[0]);
                        }
                        toRemove.Add(assignments[0]);
                        var usagePin = node.VarPin.LinkedTo.First(o => o.ParentNode is not K2Node_AssignmentStatement);
                        usagePin.Value = assignments[0].ValuePin.Value;
                        foreach (var graphPin in assignments[0].ValuePin.LinkedTo)
                            Connect(graphPin, usagePin);
                    }
                }
            }
            
            foreach (var node in toRemove)
                Graph.RemoveNode(node);
            
            if (Graph.Nodes.Where(o => o is K2Node_TemporaryVariable or K2Node_AssignmentStatement) is {} tempNodes && tempNodes.Any())
                _result.AddProblem($"Found direct local variables calls. " +
                                   $"There is probably some unknown macro or unsupported special node.\n" +
                                   $"{string.Join(", ", tempNodes.Select(o => o.GetFirstOutputParam()?.Property?.Name ?? "_"))}", null, false);

            return _result;
        }

        private void DecompileUbergraph()
        {
            ProcessUbergraphBeginning();

            List<UFunction> eventsList = [.. GlobalContext.CurrentAsset.SortedEvents];
            if (Settings.Instance.SelectedEventFirst)
            {
                eventsList.Remove(GlobalContext.FunctionToDecompile);
                eventsList.Insert(0, GlobalContext.FunctionToDecompile);
            }
            foreach (var inputEvent in GlobalContext.CurrentAsset.InputEvents)
                eventsList.RemoveAll(o => o.Name == inputEvent.FunctionName);
            foreach (var inputEvent in GlobalContext.CurrentAsset.Timelines)
                eventsList.RemoveAll(o => o.Name == inputEvent.UpdateFunctionName || o.Name == inputEvent.FinishedFunctionName);

            // preloading all ubergraph properties
            GlobalContext.FunctionLocals.AddRange(GetStructProperties(GlobalContext.CurrentAsset.UbergraphFunction!));
            
            LocalVariablesStorage tempVars = new();
            InitTempVariables(tempVars);
            
            Dictionary<UFunction, uint> entryPoints = [];
            foreach (UFunction func in GlobalContext.CurrentAsset.SortedEvents)
                entryPoints.Add(func, GetUbergraphEntryPoint(func.ScriptBytecode));
            
            ControlFlowContext flowContext = new();
            flowContext.Flow.EntryPoints = entryPoints.Values.ToList();
            flowContext.Flow.DecompileFlow(GlobalContext.CurrentFunction.ScriptBytecode);
            
            // Decompiling every timeline before events so it will be possible to connect to it
            foreach (TimelineData timeline in GlobalContext.CurrentAsset.Timelines)
            {
                K2Node_Timeline entryNode = new K2Node_Timeline(timeline.VariableName, timeline.FloatTracks, null);
                Graph.AddNode(entryNode);
                LocalVariablesStorage localVars = new();
                foreach (var tempVar in tempVars.GetLocalVars())
                    localVars.Create(tempVar.VarName, tempVar.ParamPin);
                
                localVars.Create(timeline.DirectionPropertyName, entryNode.Direction);

                foreach (var track in timeline.FloatTracks)
                {
                    localVars.Create(track.PropertyName, entryNode.Tracks[track.TrackName]);
                }
                
                UFunction updateFunc = GlobalContext.CurrentAsset.SortedEvents.Find(o => o.Name.ToString() == timeline.UpdateFunctionName)!;
                UFunction finishedFunc = GlobalContext.CurrentAsset.SortedEvents.Find(o => o.Name.ToString() == timeline.FinishedFunctionName)!;
                StartDecompilation(localVars.Clone(), entryNode.Update, flowContext, entryPoints[updateFunc]);
                StartDecompilation(localVars, entryNode.Finished, flowContext, entryPoints[finishedFunc]);
            }
            
            // Decompiling every graph events
            foreach (UFunction func in eventsList)
            {
                LocalVariablesStorage localVars = GetEventParamPins(func);
                BPNode entryNode = new K2Node_Event(func.Name, localVars.GetLocalPins(), null);
                Graph.AddNode(entryNode);
                foreach (var tempVar in tempVars.GetLocalVars())
                    localVars.Create(tempVar.VarName, tempVar.ParamPin);
                StartDecompilation(localVars, entryNode.ExecOutPin!, flowContext, entryPoints[func]);
            }

            Dictionary<string, BPNode> inputEventEntries = [];
            foreach (var data in GlobalContext.CurrentAsset.InputEvents.Where(o => o.FunctionName != "None"))
            {
                UFunction func = GlobalContext.CurrentAsset.SortedEvents.Find(o => o.Name.ToString() == data.FunctionName)!;
                LocalVariablesStorage localVars = GetEventParamPins(func);
                BPNode entryNode;
                if (inputEventEntries.TryGetValue(data.Name, out BPNode? node))
                    entryNode = node;
                else
                {
                    entryNode = inputEventEntries.GetValueOrDefault(data.Name) ?? data.Type switch
                    {
                        InputEventType.EnhancedInputAction =>
                            new K2Node_EnhancedInputAction(func.Name, data.Name, localVars.GetLocalPins(), null),
                        InputEventType.InputAction =>
                            new K2Node_InputAction(func.Name, data.Name, localVars.GetLocalPins(), null),
                        InputEventType.InputAxisAction =>
                            new K2Node_InputAxisEvent(func.Name, data.Name, localVars.GetLocalPins(), null),
                        InputEventType.Key =>
                            new K2Node_InputKey(func.Name, data.Name, localVars.GetLocalPins(), null),
                        InputEventType.InputAxisKey =>
                            new K2Node_InputKey(func.Name, data.Name, localVars.GetLocalPins(), null),
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    Graph.AddNode(entryNode);
                    inputEventEntries.Add(data.Name, entryNode);
                }
                
                int pinIndex = data.PinType switch
                {
                    InputEventPinType.Pressed => 0,
                    InputEventPinType.Released => 1,
                    InputEventPinType.Triggered => 0,
                    InputEventPinType.Started => 1,
                    InputEventPinType.Ongoing => 2,
                    InputEventPinType.Canceled => 3,
                    InputEventPinType.Completed => 4,
                    InputEventPinType.AxisExec => 0,
                    _ => throw new ArgumentOutOfRangeException()
                };

                var jump = flowContext.Flow.GetEntryPointJump(entryPoints[func]);
                int startOffset = 0;
                GraphPin[] inputPins = [.. entryNode.Output.Where(o => o.PinType.PinCategory != PinType.exec)];
                
                // remove temp variables at the start
                // looks bad but i can't find a way other than hardcode it
                if (data.Type == InputEventType.Key)
                {
                    if (jump.Destination.Instructions[0] is EX_Let let && ToName(let.Property).StartsWith("Temp_struct_Variable"))
                    {
                        BindTempInputVars(ToName(let.Property), tempVars, inputPins, 0);
                        GlobalContext.MarkAsParsed(let.StatementIndex);
                        startOffset = 1;
                    }
                }
                else if (data.Type == InputEventType.EnhancedInputAction)
                {
                    if (jump.Destination.Instructions[0] is EX_LetBool let1 &&
                        jump.Destination.Instructions[1] is EX_LetBool let2 &&
                        jump.Destination.Instructions[2] is EX_Let let3 &&
                        jump.Destination.Instructions[3] is EX_Let let4 &&
                        jump.Destination.Instructions[4] is EX_Let let5 &&
                        jump.Destination.Instructions[5] is EX_Let let6 &&
                        jump.Destination.Instructions[6] is EX_LetObj let7)
                    {
                        BindTempInputVars(VarInstrToName(let2.Variable), tempVars, inputPins, 0);
                        BindTempInputVars(VarInstrToName(let3.Variable), tempVars, inputPins, 1);
                        BindTempInputVars(VarInstrToName(let5.Variable), tempVars, inputPins, 2);
                        BindTempInputVars(VarInstrToName(let7.Variable), tempVars, inputPins, 3);
                        GlobalContext.MarkAsParsed(let1.StatementIndex);
                        GlobalContext.MarkAsParsed(let2.StatementIndex);
                        GlobalContext.MarkAsParsed(let3.StatementIndex);
                        GlobalContext.MarkAsParsed(let4.StatementIndex);
                        GlobalContext.MarkAsParsed(let5.StatementIndex);
                        GlobalContext.MarkAsParsed(let6.StatementIndex);
                        GlobalContext.MarkAsParsed(let7.StatementIndex);
                        startOffset = 7;
                    }
                }

                foreach (var tempVar in tempVars.GetLocalVars())
                    localVars.Create(tempVar.VarName, tempVar.ParamPin);

                jump.StartIndex = startOffset;
                StartDecompilation(localVars, entryNode.Output[pinIndex], flowContext, entryPoints[func]);
            }
            
            
            void BindTempInputVars(string tempVarName, LocalVariablesStorage localVariablesStorage, GraphPin[] localVars,
                int index)
            {
                var tempVar = localVariablesStorage.GetLocalVars().FirstOrDefault(o => o.VarName == tempVarName);
                // remove K2Node_TemporaryVariable node
                if (tempVar.ParamPin.ParentNode is K2Node_TemporaryVariable node)
                    Graph.RemoveNode(node);
                tempVar.ParamPin = localVars[index];
            }
        }

        private void InitTempVariables(LocalVariablesStorage localVars)
        {
            foreach (var tempVarProp in GlobalContext.FunctionLocals.Where(o => o.IsTempVar()))
            {
                // can have only one node per each temporary variable (disallow duplicates)
                K2Node_TemporaryVariable temp;
                if (Graph.Nodes.FirstOrDefault(o => 
                        o is K2Node_TemporaryVariable t && t.VarName == tempVarProp.Name)
                    is K2Node_TemporaryVariable tempNode)
                {
                    temp = tempNode;
                }
                else
                {
                    temp = new(tempVarProp, null);
                    Graph.AddNode(temp);
                }
                GraphPin tempVarPin = temp.GetFirstOutputParam()!;
                localVars.Create(tempVarProp.Name, tempVarPin);
            }
        }

        private void StartDecompilation(LocalVariablesStorage localVars, GraphPin entryPin, ControlFlowContext flowContext, uint? entryPoint = null)
        {
            DecompilerContext context = new(this, GlobalContext, entryPin, localVars, flowContext, entryPoint);
            ProcessInstructions(context);
        }

        // Get all instructions which was not reached during decompilation due to some errors probably
        private void CheckUnreachedPoints()
        {
            var unreachedInstrs = GlobalContext.CurrentFunction.ScriptBytecode.Where(o => o is not EX_EndOfScript && !GlobalContext.ParsedInstructions.Contains(o.StatementIndex));

            if (unreachedInstrs.Any())
                _result.AddProblem($"Found unreached instructions ({string.Join(", ", unreachedInstrs.Select(o => o.StatementIndex))})", null, false);
        }

        // Init function params and return params pins
        private LocalVariablesStorage ParseFunctionArguments(UFunction function)
        {
            LocalVariablesStorage localVars = new();

            foreach (PropertyData property in GetStructProperties(function))
            {
                GlobalContext.FunctionLocals.Add(property);

                if (!property.IsFunctionParam()) continue;

                bool isInput = property.IsInputParam();

                GraphPin pin = new GraphPin(property.Name, !isInput, property.PinType);

                if (isInput)
                {
                    localVars.Create(property.Name, pin);
                }
                else
                {
                    localVars.CreateOut(property.Name, pin);
                }
            }
            return localVars;
        }

        // Validate ubergraph beginning
        // Structure:
        // 1 - Push return address to stack (optional)
        // 2 - Jump to EntryPoint instruction
        private void ProcessUbergraphBeginning()
        {
            GlobalContext.MarkAsParsedAndCanVisitAgain(0);
            var script = GlobalContext.CurrentFunction.ScriptBytecode;
            if (script[0] is EX_PushExecutionFlow && script[1] is EX_ComputedJump)
            {
                GlobalContext.MarkAsParsedAndCanVisitAgain(script[1].StatementIndex);
            }
            else if (script[0] is not EX_ComputedJump)
            {
                throw new DecompilerException("Jump to ubergraph entry point not found");
            }
        }

        // Init event params pins
        // Event params have their own names in ubergraph. We get them from setters before calling ubergraph
        // Params can only be inputs
        private LocalVariablesStorage GetEventParamPins(UFunction eventFunc)
        {
            LocalVariablesStorage localVars = new();
            foreach (KismetExpression e in eventFunc.ScriptBytecode)
            {
                if (e is not EX_LetValueOnPersistentFrame let) break;

                string nameInUbergraph = ToName(let.DestinationProperty);
                PropertyData propInUbergraph = GlobalContext.FunctionLocals.Find(o => o.Name.EqualsFName(nameInUbergraph)) ??
                    throw new DecompilerException($"Failed to find ubergraph property {nameInUbergraph}");

                string name = VarInstrToName(let.AssignmentExpression);
                GraphPin pin = new GraphPin(name, false, propInUbergraph.PinType);
                localVars.Create(nameInUbergraph, pin);
            }
            return localVars;
        }

        // Decompiling kismet bytecodes into nodes
        public void ProcessInstructions(DecompilerContext context)
        {
            LocalVars = context.LocalVars;

            while (true)
            {
                while (context.BlockIndex < context.Block.Instructions.Count)
                {
                    _updateProgress?.Invoke(GlobalContext.ParsedInstructions.Count, GlobalContext.CurrentFunction.ScriptBytecode.Length);

                    var instr = context.GetInstr();

                    if (instr is EX_Return)
                        break;

                    // if statement is already parsed just connect to corresponding node
                    if (!GlobalContext.CanVisitThisInstruction(instr.StatementIndex))
                    {
                        BPNode? node = FindNearestNode(instr.StatementIndex);
                        if (node != null)
                            Connect(context.LastPin, node.ExecPin);
                        return;
                    }

                    context.MarkAsParsed();

                    if (context.BlockIndex == context.Block.Instructions.Count - 1)
                        break;

                    if (instr is EX_PushExecutionFlow)
                    {
                        var seq = context.Block.GetSequence(context.BlockIndex);
                        SequenceStatement sequence = new(context, seq);
                        context.ControlFlow.Sequences.Add(seq);
                        sequence.Decompile();
                        return;
                    }

                    // complex control flow nodes
                    if (CheckForComplexControlFlowNodes(context))
                        return;

                    if (instr is EX_Context c)
                    {
                        if (c.ContextExpression is EX_FinalFunction f)
                        {
                            (string name, string outer) = f.GetNameAndOuter();
                            if (outer == "TimelineComponent")
                            {
                                var contextPin = ArgToPin(c.ObjectExpression);
                                if (Graph.Nodes.OfType<K2Node_Timeline>()
                                        .FirstOrDefault(o => o.Name == contextPin.LinkedTo.FirstOrDefault()?.PinName) is { } node)
                                {
                                    Graph.RemoveNode(contextPin.LinkedTo[0].ParentNode);
                                    GraphPin? nextPin = name switch
                                    {
                                        "Play" => node.Play,
                                        "PlayFromStart" => node.PlayFromStart,
                                        "Stop" => node.Stop,
                                        "Reverse" => node.Reverse,
                                        "ReverseFromEnd" => node.ReverseFromEnd,
                                        "SetNewTime" => node.SetNewTime,
                                        _ => null
                                    };
                                    if (nextPin != null)
                                    {
                                        if (name == "SetNewTime")
                                        {
                                            GraphPin newTimePin = ArgToPin(f.Parameters[0]);
                                            node.NewTime.Value = newTimePin.Value;
                                            if (newTimePin.IsConnected)
                                            {
                                                Connect(node.NewTime, newTimePin.LinkedTo[0]);
                                                newTimePin.LinkedTo[0].Disconnect(newTimePin);
                                            }
                                        }
                                        Connect(context.LastPin, nextPin);
                                        context.MarkAsParsed();
                                        context.BlockIndex++;
                                        continue;
                                    }
                                }
                            }
                        }
                        
                    }
                    
                    // nodes that consists of multiple instructions
                    if (CheckForMultiInstrNodes(context))
                        continue;

                    // nodes that consists of one instruction
                    BPNode? newNode = InstrToNodes(instr);
                    context.BlockIndex++;

                    if (newNode == null) continue;

                    Graph.AddNode(newNode);
                    if (!newNode.Pure && newNode.ExecOutPin != null)
                    {
                        Connect(context.LastPin, newNode.ExecPin);
                        context.LastPin = newNode.ExecOutPin;
                    }
                }


                switch (context.Block.Type)
                {
                    case BlockType.Return:
                        new ReturnStatement(context).Decompile();
                        return;
                    case BlockType.BranchEnd:
                        return;
                    case BlockType.BranchEndIfNot:
                    case BlockType.JumpIfNot:
                        new IfStatement(context).Decompile();
                        return;
                    case BlockType.JumpToEntryPoint:
                        var jump = context.ControlFlow.Flow.GetEntryPointJump(context.EntryPoint!.Value);
                        context.ResolveJump(jump);
                        continue;
                    case BlockType.Jump:
                        context.ResolveJump(context.Block.Jumps[0]);
                        continue;
                    case BlockType.LatentAction:
                        var node = Graph.Nodes.Last();
                        GraphPin? latentPin = node.Input.Find(o =>
                            o.Property is { PinType: { PinCategory: PinType.Struct, PinSubCategoryObject: "LatentActionInfo" } });
                        latentPin?.HidePin();

                        if (context.Block.Jumps.Count == 1)
                        {
                            context.ResolveJump(context.Block.Jumps[0]);
                            continue;
                        }
                        return;
                    default:
                        throw new DecompilerException("Unknown block type", context);
                }
            }
        }

        // Returns the nearest node to the expression by array index
        private BPNode? FindNearestNode(int index)
        {
            int indexInArray = GlobalContext.ParsedInstructions.IndexOf(index);

            int count = GlobalContext.ParsedInstructions.Count;
            while (indexInArray + 1 < count)
            {
                int StatementIndex = GlobalContext.ParsedInstructions[indexInArray];
                BPNode? node = Graph.FindConnectableNode(StatementIndex);

                if (node != null)
                    return node;

                indexInArray++;
            }

            return null;
        }

        public List<GraphPin> ParseArgs(KismetExpression[] parameters, List<PropertyData> properties)
        {
            List<GraphPin> args = [];

            for (int i = 0; i < properties.Count; i++)
            {
                PropertyData property = properties[i];

                if (!property.IsFunctionParam()) continue;

                GraphPin pin;

                if (property.IsInputParam())
                {
                    pin = ArgToPin(parameters[i], property.Name);
                    
                    // hide self target pin
                    if (i == 0 && parameters[i] is EX_Self)
                    {
                        pin.HidePin();
                    }
                }
                else
                {
                    pin = new GraphPin(property.Name, EEdGraphPinDirection.EGPD_Output, property.PinType);

                    if (i < parameters.Length && parameters[i] is EX_LocalVariable)
                    {
                        LocalVars.Create(VarInstrToName(parameters[i]), pin);
                    }
                }

                if (property.PinType.IsReference)
                    pin.PinType.IsReference = true;
                
                pin.Property ??= property;

                args.Add(pin);
            }

            return args;
        }

        // Get input parameter pin
        public GraphPin ArgToPin(KismetExpression ex, string newPinName = "")
        {
            if (ParseConstExpr(ex, GlobalContext.Game, out string value, out GraphPinType pinType))
            {
                return new GraphPin(newPinName, value, EEdGraphPinDirection.EGPD_Input, pinType);
            }

            GraphPin valueVarPin;
            PropertyData? property = null;

            switch (ex)
            {
                case EX_Context:
                case EX_StructMemberContext:
                    {
                        ParseContext(ex, out GraphPin contextPin, out property);
                        
                        string name = ex is EX_StructMemberContext ? StructMemberNameToFriendlyName(property.Name) : property.Name;
                        K2Node_VariableGet getNode = new K2Node_VariableGet(property, name, contextPin, ex);
                        Graph.AddNode(getNode);
                        valueVarPin = getNode.GetFirstOutputParam()!;
                        break;
                    }
                case EX_InterfaceContext exp:
                    return ArgToPin(exp.InterfaceValue, newPinName);
                case EX_CallMath exp:
                    {
                        BPNode funcCall = InstrToNodes(exp)!;
                        Graph.AddNode(funcCall);
                        if (funcCall.OutputParamsCount != 1)
                            throw new DecompilerException($"EX_CallMath is used as function parameter, only 1 out parameter expected, got {funcCall.OutputParamsCount}. Node - {funcCall.Name}");

                        valueVarPin = funcCall.GetFirstOutputParam()!;
                        break;
                    }
                case EX_ArrayGetByRef exp:
                    {
                        GraphPin arrayPin = ArgToPin(exp.ArrayVariable);
                        GraphPin indexPin = ArgToPin(exp.ArrayIndex);
                        K2Node_GetArrayItem node = new K2Node_GetArrayItem(arrayPin, indexPin, exp);
                        Graph.AddNode(node);
                        property = arrayPin.Property;
                        valueVarPin = node.VarPin;
                        break;
                    }
                case EX_SwitchValue exp:
                    {
                        GraphPin indexPin = ArgToPin(exp.IndexTerm);
                        List<GraphPin> cases = [];
                        foreach (FKismetSwitchCase caseExpr in exp.Cases)
                        {
                            var casePin = ArgToPin(caseExpr.CaseIndexValueTerm);
                            string caseValue = casePin.Value;
                            var caseTermPin = ArgToPin(caseExpr.CaseTerm, caseValue);
                            cases.Add(caseTermPin);
                            property ??= caseTermPin.Property;
                        }
                        K2Node_Select selectNode = new K2Node_Select(indexPin, cases, exp);
                        Graph.AddNode(selectNode);
                        valueVarPin = selectNode.GetFirstOutputParam()!;
                        break;
                    }
                case EX_VariableBase exp:
                    PropertyData valueVar = VarInstrToProperty(ex, GlobalContext);

                    if (ex is EX_LocalVariable or EX_LocalOutVariable or EX_InstanceVariable && LocalVars.TryFind(valueVar.Name, out LocalVar? localVar))
                    {
                        if (localVar!.IsDirectValue)
                        {
                            var pin = localVar.ParamPin.Clone();
                            pin.SetName(newPinName);
                            return pin;
                        }

                        valueVarPin = localVar.ParamPin;
                    }
                    else if (valueVar.IsTempVar() && (valueVar.PinType.ContainerType == EPinContainerType.Array || valueVar.PinType.ContainerType == EPinContainerType.Map))
                    {
                        // Temp array or map variable without assignment is possible in select node and maybe somewhere else
                        return new GraphPin(newPinName, "", EEdGraphPinDirection.EGPD_Input, valueVar.PinType, valueVar);
                    }
                    else
                    {
                        // Temp variables should have assigned value before accessing it
                        if (valueVar.IsTempVar())
                            _result.AddProblem($"Temp variable value is not assigned. Var - {valueVar.Name} (instr - {ex.StatementIndex})", null, false);
                        // Instance variable
                        K2Node_VariableGet getNode = new K2Node_VariableGet(valueVar, valueVar.Name, null, exp);
                        Graph.AddNode(getNode);
                        valueVarPin = getNode.VarPin;
                    }
                    property = valueVar;
                    break;
                default:
                    _result.AddProblem($"ArgToPin: unknown instruction of type {ex.GetType()} (instr - {ex.StatementIndex})", null, false);
                    // Fallback
                    return new GraphPin(newPinName, "#ERROR#", EEdGraphPinDirection.EGPD_Input, MakePinType(EngineBPData.PinType.Unknown), property);
            }


            GraphPin Pin = new GraphPin(newPinName, "", EEdGraphPinDirection.EGPD_Input, valueVarPin.PinType, property);
            Connect(valueVarPin, Pin);
            return Pin;
        }

        private BPNode? InstrToNodes(KismetExpression instr, UObject? targetConst = null, string targetConstPathName = "")
        {
            switch (instr)
            {
                case EX_FinalFunction finalFunc: // also EX_CallMath
                    {
                        var func = FindFunctionInAsset(finalFunc.StackNode.ResolvedObject.Outer.Load(), finalFunc.StackNode.ResolvedObject.Outer.GetPathName(),finalFunc.StackNode.Name);

                        List<GraphPin> parms = ParseArgs(finalFunc.Parameters, func!.Params);

                        if (finalFunc is EX_CallMath &&
                            finalFunc.Parameters.Length == 2 &&
                            func.Outer.Name
                                is "/Script/Engine.KismetMathLibrary"
                                or "/Script/Engine.KismetInputLibrary"
                                or "/Script/Engine.KismetSystemLibrary"
                                or "/Script/Engine.InputDeviceLibrary" &&
                            PromotableOperators.Any(o => func.Name.Starts($"{o.Key}_")))
                            return new K2Node_PromotableOperator(func.Name, parms, instr);

                        if (func.Outer.Name == "/Script/Engine.KismetArrayLibrary" && func.Name.Starts("Array_"))
                            return new K2Node_CallArrayFunction(func, parms, instr, func.IsPure);
                        
                        return new K2Node_CallFunction(func.Name, func.Outer.Name, parms, instr, func.IsPure);
                    }
                case EX_VirtualFunction virtualFunc:
                    {
                        string funcName = virtualFunc.VirtualFunctionName.ToString();

                        // if not specified then target is self
                        if (targetConst == null)
                        {
                            targetConst = GlobalContext.CurrentAsset.GeneratedClass;
                            targetConstPathName = targetConst!.GetPathName();
                        }

                        FunctionData? func = FindFunctionInAsset(targetConst, targetConstPathName, funcName);
                        
                        List<GraphPin> parms = ParseArgs(virtualFunc.Parameters, func!.Params);

                        return new K2Node_CallFunction(funcName, targetConst.GetPathName(), parms, instr, func.IsPure);
                    }
                case EX_CallMulticastDelegate callDelegate:
                    {
                        DelegateData data = GetDelegateInfo(callDelegate.Delegate, null);
                        var func = FindFunctionInAsset(data.SignatureObject, data.SignaturePath, data.SignatureName);
                        List<PropertyData> properties = func!.Params;
                        List<GraphPin> parms = ParseArgs(callDelegate.Parameters, properties);
                        return new K2Node_CallDelegate(data, parms, instr);
                    }
                case EX_ClearMulticastDelegate clearDelegate:
                    {
                        DelegateData data = GetDelegateInfo(clearDelegate.DelegateToClear, null);
                        return new K2Node_ClearDelegate(data, instr);
                    }
                case EX_AddMulticastDelegate addDelegate:
                    {
                        DelegateData data = GetDelegateInfo(addDelegate.Delegate, addDelegate.DelegateToAdd);
                        return new K2Node_AddDelegate(data, instr);
                    }
                case EX_RemoveMulticastDelegate removeDelegate:
                    {
                        DelegateData data = GetDelegateInfo(removeDelegate.Delegate, removeDelegate.DelegateToAdd);
                        return new K2Node_RemoveDelegate(data, instr);
                    }
                case EX_BindDelegate bindDelegate:
                    {
                        DelegateData data = GetDelegateInfo(bindDelegate.Delegate, null);
                        string eventName = bindDelegate.FunctionName.ToString();

                        if (LocalVars.TryFind(data.Name, out LocalVar? var))
                        {
                            var!.ParamPin.Value = eventName;
                        }
                        else
                        {
                            GraphPin pin = new GraphPin("", eventName, EEdGraphPinDirection.EGPD_Input, MakePinType(PinType.Delegate));
                            LocalVars.Create(data.Name, pin);
                        }
                        return null;
                    }
                case EX_Let let:
                    {
                        return ProcessLetNode(instr, let.Variable, let.Assignment);
                    }
                case EX_LetBase letBase:
                    {
                        return ProcessLetNode(instr, letBase.Variable, letBase.Assignment);
                    }
                case EX_Context context:
                    {
                        GraphPin contextPin = ArgToPin(context.ObjectExpression, "Target");

                        UObject? target = null;
                        string? targetPath = null;
                        if (!contextPin.IsConnected)
                        {
                            if (context.ObjectExpression is EX_ObjectConst objConst)
                            {
                                target = objConst.Value.ResolvedObject.Class.Load();
                                targetPath = objConst.Value.ResolvedObject.Class.GetPathName();
                            }
                            contextPin.HidePin();
                        }
                        else
                        {
                            if (contextPin.Property.PropertyClassPackageIndex == null)
                            {
                                target = new UScriptClass(contextPin.Property.PinType.PinSubCategoryObject);
                                targetPath = contextPin.Property.PinType.PinSubCategoryObject;
                            }
                            else
                            {
                                target = contextPin.Property.PropertyClassPackageIndex.Load();
                                targetPath = contextPin.Property.PropertyClassPackageIndex.ResolvedObject.GetPathName();
                            }
                        }

                        if (InstrToNodes(context.ContextExpression, target, targetPath) is not BPNode Node)
                            throw new DecompilerException($"InstrToNodes: ContextExpression was not decompiled to node - {context.ContextExpression.StatementIndex}");

                        Node.NodeInstr = context;

                        // if function has target pin, it overrides from context
                        // if not, create it
                        GraphPin? targetPin = Node.GetFirstInputParam();
                        if (targetPin is { Value: "self" })
                        {
                            Node.SetInputPin(contextPin, 0);
                        }
                        else
                        {
                            Node.AddInputPin(contextPin, 0);
                        }

                        return Node;
                    }
                case EX_SetArray setArray:
                    {
                        PropertyData assignProp = VarInstrToProperty(setArray.AssigningProperty!, GlobalContext);
                        List<GraphPin> elements = [.. setArray.Elements.Select(o => ArgToPin(o))];
                        K2Node_MakeArray node = new K2Node_MakeArray(elements, assignProp.PinType, instr);
                        LocalVars.Create(assignProp.Name, node.GetFirstOutputParam()!);
                        return node;
                    }
                case EX_SetMap setMap:
                    {
                        PropertyData assignProp = VarInstrToProperty(setMap.MapProperty, GlobalContext);
                        List<GraphPin> elements = [.. setMap.Elements.Select(o => ArgToPin(o))];
                        K2Node_MakeMap node = new K2Node_MakeMap(elements, assignProp.PinType, instr);
                        LocalVars.Create(assignProp.Name, node.GetFirstOutputParam()!);
                        return node;
                    }
                default:
                    _result.AddProblem($"InstrToNodes: unknown instruction of type {instr.GetType()} (instr - {instr.StatementIndex})", null, false);
                    return new UnknownNode(instr.Token.ToString(), instr);
            }
        }
        
        private FunctionData? FindFunctionInAsset(UObject? obj, string pathName, string funcName)
        {
            if (obj is UScriptClass || (obj is null && pathName.Starts("/Script/")))
            {
                return GlobalContext.Game.Jmap.GetFunctionData(pathName, funcName);
            }

            if (obj is UClass cl)
            {
                var func = cl.FuncMap.FirstOrDefault(o => o.Key.Text == funcName).Value?.Load() as UFunction;
                if (func is { })
                    return ToFuncData(func, cl);
                if (cl.SuperStruct is null) return null;
                if (FindFunctionInAsset(cl.SuperStruct.Load(), cl.SuperStruct.ResolvedObject.GetPathName(), funcName) is { } f)
                    return f;
                foreach (var fImplementedInterface in cl.Interfaces)
                {
                    var i = fImplementedInterface.Class.Load();
                    if (FindFunctionInAsset(i, i.GetPathName(), funcName) is { } f2)
                        return f2;
                }
            }

            return null;

            FunctionData ToFuncData(UFunction func, UObject outer)
            {
                return new FunctionData(func.Name, func.FunctionFlags)
                {
                    Params = GetStructProperties(func),
                    Outer = new ObjectData(outer)
                };
            }
        }

        private DelegateData GetDelegateInfo(KismetExpression delegateExpr, KismetExpression? delegateToAddExpr)
        {
            GetVarWithTarget(delegateExpr, out PropertyData Delegate, out GraphPin? contextInputPin);
            contextInputPin ??= new GraphPin("Target", "self", EEdGraphPinDirection.EGPD_Input, MakePinType(PinType.Object));

            DelegateData data = new()
            {
                Name = Delegate.Name,
                ContextInputPin = contextInputPin,
                Owner = Delegate.Owner,
                OwnerObject = Delegate.OwnerObject,
                SignatureName = Delegate.DelegateSignatureFunction,
                SignaturePath = Delegate.DelegateSignatureObjectPath,
                SignatureObject = Delegate.DelegateSignatureObject
            };

            if (delegateToAddExpr != null)
            {
                string eventName = VarInstrToName(delegateToAddExpr);
                data.Delegate = LocalVars.Find(eventName).ParamPin;
            }

            return data;
        }

        // Get variable or context property and target pin
        private void GetVarWithTarget(KismetExpression ex, out PropertyData property, out GraphPin? contextInputPin)
        {
            if (ex is EX_Context or EX_StructMemberContext)
            {
                ParseContext(ex, out contextInputPin, out property);
            }
            else
            {
                contextInputPin = null;
                property = VarInstrToProperty(ex, GlobalContext);
            }
        }

        // Get context property and target pin
        private void ParseContext(KismetExpression expr, out GraphPin contextPin, out PropertyData property)
        {
            switch (expr)
            {
                case EX_Context context:
                    {
                        if (context.ContextExpression is not EX_VariableBase)
                        {
                            throw new DecompilerException($"ParseContext: ContextExpression isn't variable - {context.StatementIndex}");
                        }
                        contextPin = ArgToPin(context.ObjectExpression, "Target");
                        property = VarInstrToProperty(context.ContextExpression, GlobalContext);
                        return;
                    }
                case EX_StructMemberContext context:
                    {
                        contextPin = ArgToPin(context.StructExpression);
                        property = KismetPointerToProperty(context.Property, GlobalContext);
                        return;
                    }
                default:
                    throw new DecompilerException($"ParseContext: unexpected instruction - {expr.StatementIndex}");
            }
        }

        private BPNode? ProcessLetNode(KismetExpression instr, KismetExpression variable, KismetExpression assignment)
        {
            if (variable is EX_ArrayGetByRef)
            {
                var variablePin = ArgToPin(variable, "Reference");
                return new K2Node_VariableSet(variablePin.Property, ArgToPin(assignment, "New value"), variablePin, instr);
            }

            GetVarWithTarget(variable, out PropertyData property, out GraphPin? contextInputPin);

            if (IsCallFuncInstr(assignment) || assignment is EX_Context context && IsCallFuncInstr(context.ContextExpression))
            {
                var node = LetFunctionValue(assignment, property);
                node.NodeInstr = instr;
                return node;
            }

            // don't make node for temp primitive cast params, just define its value
            if (assignment is EX_Cast cast)
            {
                LocalVars.Create(property.Name, ArgToPin(cast.Target).LinkedTo.First());
                return null;
            }

            GraphPin valuePin = ArgToPin(assignment, property.Name);

            // don't make node for output params, just define its value
            if (property.IsOutParam())
            {
                LocalVars.SetOut(property.Name, valuePin);
                return null;
            }

            // don't make node for temp params, just define its value
            if (property.IsTempVar())
            {
                GraphPin tempVarPin = LocalVars.Find(property.Name)!.ParamPin;

                LocalVars.Create(property.Name, tempVarPin);
                GraphPin variableIn = new("Variable", true, property.PinType);
                Connect(tempVarPin, variableIn);
                valuePin.SetName("Value");
                return new K2Node_AssignmentStatement(variableIn, valuePin, instr);
            }

            return new K2Node_VariableSet(property, valuePin, contextInputPin, instr);
        }

        private BPNode LetFunctionValue(KismetExpression ex, PropertyData prop)
        {
            BPNode? node = InstrToNodes(ex);
            if (!(node is K2Node_CallFunction or K2Node_PromotableOperator))
                throw new DecompilerException($"Set return value of non-function. Node - {node?.Name} ({ex.StatementIndex})");

            if (node.Output.Find(o => o.PinName == "ReturnValue") is not { } returnValuePin)
                throw new DecompilerException($"LetFunctionValue: failed to find ReturnValue pin for node {node.Name} ({ex.StatementIndex})");

            // ignore int as it can be pointer to object
            if (returnValuePin.PinType.PinCategory != PinType.Int && prop.PinType.PinCategory != PinType.Int && returnValuePin.PinType.PinCategory != prop.PinType.PinCategory)
                throw new DecompilerException($"LetFunctionValue: node {node.Name} return type is {returnValuePin.PinType.PinCategory} but expected is {prop.PinType.PinCategory} ({ex.StatementIndex})");

            LocalVars.Create(prop.Name, returnValuePin);
            return node;
        }
    }
}