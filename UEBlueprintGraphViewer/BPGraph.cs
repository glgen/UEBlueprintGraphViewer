using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Core.Routing;
using Microsoft.Msagl.Layout.Layered;
using Microsoft.Msagl.Miscellaneous;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CUE4Parse.Utils;
using UEBlueprintGraphViewer.Comparing;
using UEBlueprintGraphViewer.Engine;
using UEBlueprintGraphViewer.Nodes;
using static UEBlueprintGraphViewer.Engine.EngineBPData;
using Point = Microsoft.Msagl.Core.Geometry.Point;

namespace UEBlueprintGraphViewer
{
    public class BPGraph
    {
        // all nodes in graph
        public List<BPNode> Nodes = [];

        // subgraphs
        public List<List<BPNode>> Clusters = [];

        // macro input and output nodes (for macro graphs)
        public BPNode? MacroInputNode;
        public BPNode? MacroOutputNode;

        public string MacroName;
        
        public delegate void UpdateProgressDelegate(int count, int countMax);

        public void AddNode(BPNode Node)
        {
            Nodes.Add(Node);
        }

        public void RemoveNode(BPNode Node)
        {
            Nodes.Remove(Node);
            foreach (GraphPin input in Node.Input)
            {
                foreach (GraphPin linked in input.LinkedTo)
                {
                    linked.Disconnect(input);
                }
            }

            foreach (GraphPin output in Node.Output)
            {
                foreach (GraphPin linked in output.LinkedTo)
                {
                    linked.Disconnect(output);
                }
            }
        }
        
        public BPNode? FindFuncStartNode(string name)
        {
            return Nodes.Find(o => o is K2Node_FunctionEntry n && n.Name == name);
        }

        public BPNode? FindConnectableNode(int index)
        {
            return Nodes.Find(o => o.StatementIndex == index && o.ExecPin != null);
        }

        // Split graph on subgraps to layout them separately
        void FindClusters()
        {
            if (Clusters.Count > 0)
                return;

            List<BPNode> Starts = Nodes.FindAll(o => o is K2Node_Event or K2Node_FunctionEntry);
            List<BPNode> ParsedNodes = [];

            foreach (BPNode Node in Starts)
            {
                List<BPNode> cluster = [];
                Walk(Node, cluster, ParsedNodes);

                if (cluster.Count > 0)
                    Clusters.Add(cluster);
            }

            if (ParsedNodes.Count != Nodes.Count)
            {
                //throw new Exception("Failed to walk through nodes");
            }

            void Walk(BPNode node, List<BPNode> cluster, List<BPNode> ParsedNodes)
            {
                if (ParsedNodes.Exists(o => o == node))
                {
                    return;
                }

                ParsedNodes.Add(node);
                cluster.Add(node);
                IEnumerable<GraphPin> Pins = [.. node.Input, .. node.Output];

                foreach (GraphPin pin in Pins)
                {
                    foreach (GraphPin c in pin.LinkedTo)
                    {
                        if (c.ParentNode != null)
                        {
                            Walk(c.ParentNode, cluster, ParsedNodes);
                        }
                    }
                }
            }
        }
        
        Dictionary<BPNode, int> FindLayers()
        {
            Dictionary<BPNode, int> layers = Nodes.ToDictionary(o => o, o => int.MinValue);
            
            foreach (List<BPNode> cluster in Clusters)
            {
                bool found = true;
                while (found)
                {
                    found = false;
                    foreach (BPNode node in cluster.Where((o => !o.Pure)))/// && !o.HeaderHidden)))
                    {
                        if (layers[node] != int.MinValue)
                            continue;

                        HashSet<BPNode> prev = node.Input
                            .Where(o => o.PinType.PinCategory == PinType.exec)
                            .SelectMany(o => o.LinkedTo)
                            .Select(o => o.ParentNode)
                            .Where(o => !o.Pure)// && !o.HeaderHidden)
                            .ToHashSet();

                        if (prev.Count == 0)
                        {
                            layers[node] = 0;
                            continue;
                        }
                        
                        var prevl = prev.Select((o=> layers[o]));
                        if (prevl.Contains(int.MinValue))
                        {
                            prevl = prevl.Where(o => o != int.MinValue);
                            found = true;
                        }

                        if (prevl.Any())
                            layers[node] = prevl.Max() + 1;
                    }
                }

                found = true;
                while (found)
                {
                    found = false;
                    foreach (BPNode node in cluster.Where((o => o.Pure)))// || o.HeaderHidden)))
                    {
                        if (layers[node] != int.MinValue)
                            continue;

                        HashSet<BPNode> next = node.Output
                            .SelectMany(o => o.LinkedTo)
                            .Select(o => o.ParentNode)
                            .ToHashSet();
                        
                        var nextl = next.Select((o=> layers[o]));
                        if (nextl.Contains(int.MinValue))
                        {
                            nextl = nextl.Where(o => o != int.MinValue);
                            found = true;
                            //continue;
                        }
                        
                        if (nextl.Any())
                            layers[node] = nextl.Min() - 1;
                        else
                            layers[node] = 0;
                    }
                }
            }

            return layers;
        }

        public static bool IsEquals(BPGraph graph1, BPGraph graph2)
        {
            if (graph1.Nodes.Count != graph2.Nodes.Count)
                return false;

            for (int i = 0; i < graph1.Nodes.Count; i++)
            {
                if (!graph1.Nodes[i].TestEquality(graph2.Nodes[i]) ||
                    graph2.Nodes[i].CheckIsChanged(graph1.Nodes[i]))
                    return false;
            }

            return true;
        }

        public static string[] TestUbergraphEquality(BPGraph graph1, BPGraph graph2)
        {
            HashSet<string> names = [];

            Compare(graph1, graph2);

            foreach (var node in graph1.Nodes.Where(o => o.ChangeStatus == ChangeStatus.Removed))
            {
                AddRange(graph1.GetEventNamesFromNode(node));
            }
            foreach (var node in graph2.Nodes.Where(o => o.ChangeStatus is ChangeStatus.Added or ChangeStatus.Changed))
            {
                AddRange(graph2.GetEventNamesFromNode(node));
            }

            return [.. names];

            void AddRange(string[] items)
            {
                foreach (var str in items)
                    names.Add(str);
            }
        }

        public string[] GetEventNamesFromNode(BPNode node)
        {
            if (Clusters.Count == 0)
                FindClusters();

            return Clusters.Find(o => o.Contains(node))!.Where(o => o is K2Node_FunctionEntry).Select(o => o.Name).ToArray();
        }

        public List<BPNode>? GetClusterFromFunctionName(string name)
        {
            if (Clusters.Count == 0)
                FindClusters();

            BPNode? entryNode = FindFuncStartNode(name);
            if (entryNode == null)
                return null;
            return Clusters.Find(o => o.Contains(entryNode));
        }

        public static void Compare(BPGraph graph1, BPGraph graph2)
        {
            List<BPNode> added = [];
            List<BPNode> removed = [];
            List<BPNode> changed = [];

            graph1.FindClusters();
            graph2.FindClusters();

            foreach (var cluster2 in graph2.Clusters)
            {
                var cluster1 = graph1.GetClusterFromFunctionName(cluster2[0].Name);
                if (cluster1 == null)
                {
                    added.AddRange(cluster2);
                    continue;
                }

                int index1 = 0;
                int index2 = 0;

                while (index2 < cluster2.Count)
                {
                    if (index1 >= cluster1.Count)
                    {
                        for (int i = index2; i < cluster2.Count; i++)
                            added.Add(cluster2[i]);
                        break;
                    }
                    if (index2 >= cluster2.Count)
                    {
                        for (int i = index1; i < cluster1.Count; i++)
                            removed.Add(cluster1[i]);
                        break;
                    }

                    BPNode node1 = cluster1[index1];
                    BPNode node2 = cluster2[index2];
                    if (node2.TestEquality(node1))
                    {
                        index1++;
                        index2++;
                        if (node2.CheckIsChanged(node1))
                        {
                            changed.Add(node1);
                            changed.Add(node2);
                        }
                        continue;
                    }

                    bool foundChange = false;
                    int counter = 1;
                    while (index2 + counter < cluster2.Count)
                    {
                        if (cluster2[index2 + counter].TestEquality(node1))
                        {
                            foundChange = true;
                            break;
                        }
                        counter++;
                    }

                    int counter2 = 1;
                    bool foundChange2 = false;
                    while (index1 + counter2 < cluster1.Count)
                    {
                        if (cluster1[index1 + counter2].TestEquality(node2))
                        {
                            foundChange2 = true;
                            break;
                        }
                        counter2++;
                    }

                    if (foundChange2 && (foundChange && counter2 < counter || !foundChange))
                    {
                        for (int i = 0; i < counter2; i++)
                        {
                            removed.Add(cluster1[index1 + i]);
                        }
                        index1 += counter2;
                    }
                    else if (foundChange)
                    {
                        for (int i = 0; i < counter; i++)
                        {
                            added.Add(cluster2[index2 + i]);
                        }
                        index2 += counter;
                    }
                    else
                    {
                        added.Add(node2);
                        removed.Add(node1);
                    }

                    index1++;
                    index2++;
                }
            }

            foreach (var node in added)
                node.ChangeStatus = ChangeStatus.Added;

            foreach (var node in removed)
                node.ChangeStatus = ChangeStatus.Removed;

            foreach (var node in changed)
                node.ChangeStatus = ChangeStatus.Changed;
        }

        public Task LayoutNodesMsaglAsync(UpdateProgressDelegate? UpdateProgress)
        {
            return Task.Run(() => LayoutNodesMsagl(UpdateProgress) );
        }

        // Layout graph using MSAGL
        public void LayoutNodesMsagl(UpdateProgressDelegate? UpdateProgress)
        {
            FindClusters();
            
            // HACK: remove all nodes without a cluster
            // TODO: figure out a way to not generate them
            Nodes = Clusters.SelectMany(o => o).ToList();
            
            foreach (BPNode node in Nodes)
            {
                node.CalculateSize();
                node.SetPosition(0, -999);
            }

            if (Nodes.Count == 0)
                return;
            
            if (Settings.Instance.IsMSAGL)
            {
                SugiyamaLayoutSettings settings = MakeMSAGLSettings();
                
                int LayoutProgress = 0;
                
                // make MSAGL graph for each cluster
                List<GeometryGraph> graphs = [];
                foreach (List<BPNode> cluster in Clusters)
                {
                    UpdateProgress?.Invoke(LayoutProgress, Clusters.Count);
                    LayoutProgress++;
                
                    GeometryGraph Graph = BuildMSAGLGraph(cluster);
                    LayoutHelpers.CalculateLayout(Graph, settings, null);
                
                    graphs.Add(Graph);
                }
                
                // update nodes positions
                double y = 0;
                for (int i = 0; i < graphs.Count; i++)
                {
                    GeometryGraph graph = graphs[i];
                    List<BPNode> cluster = Clusters[i];
                
                    Point startPos = graph.BoundingBox.LeftBottom;
                
                    for (int ii = 0; ii < graph.Nodes.Count; ii++)
                    {
                        Point p = graph.Nodes[ii].BoundingBox.LeftBottom;
                        cluster[ii].SetPosition((float)(p.X - startPos.X), (float)(p.Y - startPos.Y + y));
                    }
                
                    y += graph.BoundingBox.Height + 100;
                
                    if (Settings.ExperimentalExecStraightening)
                    {
                        List<BPNode> mainFlowNodes = cluster.Where(o => o is K2Node_FunctionEntry || o.Input.Any(o => o.PinType.PinCategory == PinType.exec)).ToList();
                        List<BPNode> nodesToCheck = [.. mainFlowNodes];
                        foreach (BPNode node in cluster)
                        {
                            if (nodesToCheck.Contains(node))
                            {
                                List<BPNode> group = nodesToCheck.Where(o => Math.Abs((o.Y + o.NodeHeight / 2f) - (node.Y + node.NodeHeight / 2f)) < 100).ToList();
                
                                foreach (var n in group)
                                    nodesToCheck.Remove(n);
                
                                var avgY = group.Select(o => o.Y).Average();
                
                                foreach (var n in group)
                                    n.Y = avgY;
                            }
                        }
                    }
                }
            }
            else
            {
                // calculate vertical layers where functions entries starts from 0
                Dictionary<BPNode, int> layers = FindLayers();
                int minLayer = layers.Min(o => o.Value);
                int maxLayer = layers.Max(o => o.Value);
                Dictionary<int, int> layersX = [];
                int yOffset = 0;
                foreach (List<BPNode> cluster in Clusters)
                {
                    int i = minLayer;
                    int x = 0;
                    
                    // going forward from first layer to last
                    // on that step the main flow is layouted, ignoring pure nodes
                    while (i <= maxLayer)
                    {
                        // every node that have exec pins gets placed on average of previous nodes exec pins
                        // (for most of the cases there is only one connection and it is made straight)
                        // if the node does not have exec pins, it is placed at y -999
                        var nodes = cluster.Where(o => layers[o] == i);
                        var width = 0;
                        foreach (var node in nodes)
                        {
                            if (node.NodeWidth > width)
                                width = node.NodeWidth;
                            var firstExec = node.Input.FirstOrDefault(o => o.PinType.PinCategory == PinType.exec);
                            var prevPin = firstExec?.LinkedTo.Count != 0 ? firstExec?.LinkedTo.Average(o => o.Y) : null;
                            var y = (prevPin - (firstExec?.Y - node.Y)) ?? -999;
                            if (node is K2Node_FunctionEntry) y = 0;
                            node.SetPosition(x, y);
                        }
                        
                        // shift down nodes if they are colliding with each other
                        FixupY(nodes);
                        layersX[i] = x;
                        x += width + 100;
                        i++;
                    }

                    i = maxLayer;
                    
                    // going backwards from last layer to first
                    // on that step pure nodes are layouted
                    while (i >= minLayer)
                    {
                        // every node that have any output connections gets placed on average of next nodes input pins
                        var nodes = cluster.Where(o => layers[o] == i);
                        x = layersX[i];
                        foreach (var node in nodes)
                        {
                            // skip non-pure nodes since they have already been layouted
                            // pure nodes are still at y -999
                            if (node.Y != -999) continue;
                            var first = node.Output.FirstOrDefault(o => o.IsConnected);
                            var prevPin = first?.LinkedTo.Count != 0 ? first?.LinkedTo.Average(o => o.Y) : null;
                            var y = (prevPin - (first?.Y - node.Y)) ?? -999;
                            node.SetPosition(x, y);
                        }
                        
                        // shift down nodes if they are colliding with each other
                        FixupY(nodes);
                        i--;
                    }

                    var min = cluster.Min(o => o.Y + o.NodeHeight).TruncToInt();
                    
                    // shift the cluster down if we have more clusters above
                    foreach (var node in cluster)
                        node.SetPosition(node.X, node.Y - min + yOffset);
                    
                    // calculate y offset of next cluster
                    yOffset += cluster.Max(o => o.Y + o.NodeHeight).TruncToInt() - cluster.Min(o => o.Y + o.NodeHeight).TruncToInt() + 200;
                }
            }

            // shift down nodes if they are colliding with each other until they don't
            void FixupY(IEnumerable<BPNode> nodes)
            {
                var reversedNodes = nodes.Reverse();
                foreach (var node in reversedNodes)
                {
                    if (node.Y == -999) continue;
                    bool changed = true;
                    while (changed)
                    {
                        changed = false;
                        foreach (var lNode in nodes)
                        {
                            if (node == lNode) continue;
                                
                            if (node.Y <= lNode.Y + lNode.NodeHeight && node.Y + node.NodeHeight >= lNode.Y)
                            {
                                node.SetPosition(node.X, lNode.Y + lNode.NodeHeight + 50);
                                changed = true;
                            }
                        }
                    }
                }
            }

            if (Settings.DrawDebugGraph)
            {
                //GraphDebugDrawing.DrawGraph(graphs);
            }
        }

        private static SugiyamaLayoutSettings MakeMSAGLSettings()
        {
            SugiyamaLayoutSettings settings = new SugiyamaLayoutSettings();

            double num = Math.Cos(Math.PI / 2);
            double num2 = Math.Sin(Math.PI / 2);
            settings.Transformation = new PlaneTransformation(num, -num2, 0.0, -num2, num, 0.0);

            settings.NodeSeparation = 25;
            settings.LayerSeparation = 125;

            settings.BrandesThreshold = int.MaxValue;

            settings.LayeringOnly = !Settings.DrawDebugGraph;
            settings.EdgeRoutingSettings.EdgeRoutingMode = EdgeRoutingMode.StraightLine;
            return settings;
        }

        private static GeometryGraph BuildMSAGLGraph(List<BPNode> Cluster)
        {
            GeometryGraph Graph = new GeometryGraph();

            // add nodes to graph
            foreach (BPNode node in Cluster)
            {
                ICurve rect = CurveFactory.CreateRectangle(node.NodeWidth, node.NodeHeight, new Point());
                Graph.Nodes.Add(new Node(rect, node));
            }

            // add connections to graph
            for (int i = 0; i < Cluster.Count; i++)
            {
                foreach (GraphPin pin in Cluster[i].Output)
                {
                    foreach (GraphPin pin2 in pin.LinkedTo)
                    {
                        if (pin2.ParentNode == null)
                        {
                            continue;
                        }

                        Node edgeTarget = Graph.FindNodeByUserData(pin2.ParentNode);
                        Edge edge = new Edge(Graph.Nodes[i], edgeTarget);

                        if (pin2.PinType.PinCategory == PinType.exec)
                        {
                            edge.Weight = 100;
                        }

                        Graph.Edges.Add(edge);
                    }
                }
            }

            return Graph;
        }

        List<Edge2> GetEdges()
        {
            List<Edge2> edges = [];
            foreach (var bpNode in Nodes)
            {
                if (bpNode.NodeType == "K2Node_Tunnel")
                    continue;

                for (int i = 0; i < bpNode.Output.Count; i++)
                {
                    var graphPin = bpNode.Output[i];
                    for (var j = 0; j < graphPin.LinkedTo.Count; j++)
                    {
                        var pin = graphPin.LinkedTo[j];
                        if (pin.ParentNode.NodeType == "K2Node_Tunnel")
                            continue;
                        edges.Add(new()
                        {
                            From = graphPin,
                            To = pin,
                            FromNodeIndex = Nodes.IndexOf(graphPin.ParentNode),
                            ToNodeIndex = Nodes.IndexOf(pin.ParentNode),
                            FromPinIndex = i,
                            ToPinIndex = j
                        });
                    }
                }
            }
            return edges;
        }

        class Edge2
        {
            public GraphPin From;
            public GraphPin To;
            public int FromNodeIndex;
            public int ToNodeIndex;
            public int FromPinIndex;
            public int ToPinIndex;
        }

        // Find all used macros and collapse nodes into macro instance modes
        public void ProcessMacros()
        {
            foreach (var macro in Settings.Instance.Macros)
            {
                var patternNodes = macro.Value.Nodes;
                
                if (patternNodes.Count > Nodes.Count)
                    continue;
                
                Stopwatch sw = Stopwatch.StartNew();
                
                List<Edge2> edgesPattern = macro.Value.GetEdges();
                
                int[][] matrix = new int[patternNodes.Count][];

                for (int i = 0; i < patternNodes.Count; i++)
                {
                    int[] row = new int[Nodes.Count];
                    int jj = 0;
                    for (int j = 0; j < Nodes.Count; j++)
                    {
                        if (TypesEqual(patternNodes[i], Nodes[j]))
                        {
                            row[jj] = j;
                            jj++;
                        }
                    }
                    Array.Resize(ref row, jj);
                    matrix[i] = row;
                }

                bool changed = true;
                while (changed)
                {
                    changed = false;
                    
                    for (int i = 0; i < patternNodes.Count; i++)
                    {
                        foreach (var j in matrix[i])
                        {
                            List<GraphPin> neighbors1 = [];
                            foreach (var graphPin in patternNodes[i].Output)
                            {
                                neighbors1.AddRange(graphPin.LinkedTo);
                            }
                            
                            foreach (GraphPin pin in neighbors1)
                            {
                                if (pin.ParentNode.NodeType == "K2Node_Tunnel") continue;
                                
                                var k = patternNodes.IndexOf(pin.ParentNode);

                                bool haveMatch = false;
                            
                                List<GraphPin> neighbors2 = [];
                                foreach (var graphPin in Nodes[j].Output)
                                {
                                    neighbors2.AddRange(graphPin.LinkedTo);
                                }

                                foreach (GraphPin pin2 in neighbors2)
                                {
                                    var m = Nodes.IndexOf(pin2.ParentNode);
                                    if (matrix[k].Contains(m))
                                    {
                                        haveMatch = true;
                                        break;
                                    }
                                }

                                if (!haveMatch)
                                {
                                    var row = matrix[i].ToList();
                                    row.Remove(j);
                                    matrix[i] = [.. row];
                                    changed = true;
                                    break;
                                }
                            }
                        }   
                    }
                }
                
                //Trace.WriteLine(Utils.MatrixToString(matrix));
                
                List<Dictionary<int, int>> results = [];

                for (var index = 0; index < Nodes.Count; index++)
                {
                    if (results.Any(o => o.ContainsValue(index)))
                        continue;
                    var bpNode = Nodes[index];
                    if (IsThisMacroHere(matrix, bpNode, macro.Value, edgesPattern, out var foundNodes))
                        results.Add(foundNodes);
                }
                
                List<BPNode> toRemove = [];
                List<BPNode> toAdd = [];
                foreach (var result in results)
                {
                    Dictionary<GraphPin, List<GraphPin>> links = [];
                    foreach (var pin in macro.Value.MacroInputNode!.Output)
                        links.Add(pin, []);
                    foreach (var pin in macro.Value.MacroOutputNode!.Input)
                        links.Add(pin, []);
                    
                    toRemove.AddRange(result.Values.Select(o => Nodes[o]).ToList());
                    
                    foreach (var pair in result)
                    {
                        BPNode macroNode = patternNodes[pair.Key];
                        BPNode testNode = Nodes[pair.Value];

                        CheckPins(macroNode.Input, testNode.Input);
                        CheckPins(macroNode.Output, testNode.Output);
                        
                        void CheckPins(List<GraphPin> macroPins, List<GraphPin> testPins)
                        {
                            for (int i = 0; i < macroPins.Count; i++)
                            {
                                if (macroPins[i].LinkedTo.FirstOrDefault(o => o.ParentNode is {NodeType: "K2Node_Tunnel"}) is {} tunnelMacroPin)
                                {
                                    links.GetValueOrDefault(tunnelMacroPin).Add(testPins[i]);
                                }
                            }
                        }
                    }
                    
                    List<GraphPin> inputs = [.. macro.Value.MacroInputNode!.Output
                        .Select(o => MakeMacroInstancePin(o, EngineEnums.EEdGraphPinDirection.EGPD_Input, links))];
                    
                    List<GraphPin> outputs = [.. macro.Value.MacroOutputNode!.Input
                        .Select(o => MakeMacroInstancePin(o, EngineEnums.EEdGraphPinDirection.EGPD_Output, links))];
                    
                    K2Node_MacroInstance inst = new(inputs, outputs, macro.Value.MacroName, Nodes[result[0]].NodeInstr);
                    toAdd.Add(inst);
                }
                
                foreach (var node in toRemove)
                    RemoveNode(node);
                foreach (var node in toAdd)
                    AddNode(node);
                
                sw.Stop();
                
                // Trace.WriteLine($"MACRO: {macro.Key} count: {results.Count} (done in {sw.ElapsedMilliseconds} ms)");
            }

            bool TypesEqual(BPNode a, BPNode b)
            {
                if (a.NodeType != b.NodeType ||
                    a.Name != b.Name ||
                    a.Input.Count > b.Input.Count ||
                    a.Output.Count > b.Output.Count)
                    return false;
                
                for (int i = 0; i < a.Input.Count; i++)
                {
                    GraphPin pin1 = a.Input[i];
                
                    // ignore macro tunnel pins
                    if (pin1.LinkedTo.Any(o => o.ParentNode.NodeType == "K2Node_Tunnel"))
                        continue;
                
                    GraphPin pin2 = b.Input[i];
                    if (GraphPin.IsBasicallyDifferent(pin1, pin2))
                        return false;

                    if (!pin1.IsConnected &&
                        pin1.Value != pin2.Value)
                        return false;
                }
                for (int i = 0; i < a.Output.Count; i++)
                {
                    GraphPin pin1 = a.Output[i];
                
                    // ignore macro tunnel pins
                    if (pin1.LinkedTo.Any(o => o.ParentNode.NodeType == "K2Node_Tunnel"))
                        continue;
                
                    GraphPin pin2 = b.Output[i];
                    if (GraphPin.IsBasicallyDifferent(pin1, pin2))
                        return false;
                }

                return true;
            }

            
            bool IsThisMacroHere(int[][] matrix, BPNode node, BPGraph patternGraph, List<Edge2> patternEdges, out Dictionary<int, int> mappingsOut)
            {
                BPNode firstNode = patternGraph.MacroInputNode.Output.FirstOrDefault(o =>
                        o.PinType.PinCategory == PinType.exec).LinkedTo[0].ParentNode;
                
                Dictionary<int, int> mappings = [];
                mappingsOut = mappings;
                List<BPNode> failedNodes = [];
                
                // HACK: if we found nodes, but connection check fails, we try again excluding nodes with wrong connections.
                // literally trying to bruteforce the right combination of nodes if it exists.
                // this is for rare cases when there are some similar nodes that tricks recursive finding.
                while (true)
                {
                    mappings.Clear();
                    mappings[patternGraph.Nodes.IndexOf(firstNode)] = Nodes.IndexOf(node);
                    if (!(Test(firstNode, node) && mappings.Count == patternGraph.Nodes.Count - 2))
                        break;
                    
                    if (!CheckAllConnections(mappings, patternEdges, Nodes, out BPNode? failed))
                    {
                        failedNodes.Add(failed!);
                        continue;
                    }

                    return true;
                }
                return false;
                
                bool Test(BPNode macroNode, BPNode testNode)
                {
                    var row = matrix[patternGraph.Nodes.IndexOf(macroNode)];
                    var testIndex = Nodes.IndexOf(testNode);
                    if (!row.Contains(testIndex))
                        return false;

                    if (failedNodes.Contains(testNode))
                        return false;

                    int macroNodeIndex = patternGraph.Nodes.IndexOf(macroNode);
                    mappings[macroNodeIndex] = Nodes.IndexOf(testNode);
                    
                    if (CheckPins(macroNode.Output, testNode.Output))
                        return true;

                    if (CheckPins(macroNode.Input, testNode.Input))
                        return true;
                    
                    if (mappings.Count == patternGraph.Nodes.Count - 2)
                        return true;
                    
                    return false;
                    
                    bool CheckPins(List<GraphPin> macroPins, List<GraphPin> testPins)
                    {
                        for (int i = 0; i < macroPins.Count; i++)
                        {
                            foreach (var macroLink in macroPins[i].LinkedTo)
                            {
                                int macroNodeIndex = patternGraph.Nodes.IndexOf(macroLink.ParentNode);
                                if (mappings.ContainsKey(macroNodeIndex))
                                    continue;
                                foreach (var testLink in testPins[i].LinkedTo)
                                {
                                    if (Test(macroLink.ParentNode, testLink.ParentNode))
                                    {
                                        return true;
                                    }
                                }
                                
                            }
                        }
                        return false;
                    }
                }
            }

            bool CheckAllConnections(Dictionary<int, int> mapping, List<Edge2> patternEdges,
                                     List<BPNode> bigNodes, out BPNode? failedNode)
            {
                failedNode = null;
                foreach (var edge in patternEdges)
                {
                    if (!mapping.TryGetValue(edge.FromNodeIndex, out int fromId) ||
                        !mapping.TryGetValue(edge.ToNodeIndex, out int toId))
                        return false;

                    if (!HaveEdge(bigNodes[fromId], bigNodes[toId], edge, out failedNode))
                        return false;
                }

                return true;
                
                bool HaveEdge(BPNode from, BPNode to, Edge2 edge, out BPNode failedNode)
                {
                    failedNode = null;
                    var output = from.Output[edge.FromPinIndex];
                    if (!GraphPin.IsBasicallyDifferent(output, edge.From))
                    {
                        foreach (var input in output.LinkedTo)
                        {
                            if (input.ParentNode == to && !GraphPin.IsBasicallyDifferent(input, edge.To))
                                return true;
                        }

                        failedNode = to;
                        return false;
                    }

                    failedNode = from;
                    return false;
                }
            }

            static GraphPin MakeMacroInstancePin(GraphPin pin, EngineEnums.EEdGraphPinDirection direction, Dictionary<GraphPin, List<GraphPin>> links)
            {
                var newPin = new GraphPin(pin.PinFriendlyName, direction, pin.PinType)
                {
                    IsNameHidden = pin.IsNameHidden,
                };
                links.TryGetValue(pin, out var link);
                foreach (var pin2 in link ?? [])
                {
                    // set value in case the pin stores constant value
                    newPin.Value = pin2.Value;
                    foreach (var pin3 in pin2.LinkedTo)
                    {
                        // replace wildcard type with actual type
                        newPin.PinType = pin3.PinType;
                        Utils.Connect(newPin, pin3);
                    }
                }

                return newPin;
            }
        }


        // Collapse chosen nodes to macro and generate macro graph
        public static BPGraph ToMacro(List<BPNode> nodes, string name)
        {
            // Find nodes bounds coordinates
            float minX = nodes.Min(o => o.X);
            float minY = nodes.Min(o => o.Y);
            float maxX = nodes.Max(o => o.Right);
            
            List<GraphPin> outgoingPins = [];
            foreach (var node in nodes)
            {
                // Find used pins outside the macro
                foreach (var pin in node.Input)
                    CheckPin(pin);
                foreach (var pin in node.Output)
                    CheckPin(pin);
                
                // Move nodes closer to 0 coordinates
                node.SetPosition(node.X - minX, node.Y - minY);
            }
            
            // Create macro graph
            BPGraph graph = new() { MacroName = name };
            graph.Nodes.AddRange(nodes);

            // Make macro inputs and outputs pins
            graph.MacroInputNode = new K2Node_Tunnel(false, outgoingPins.Where(o => o.IsOutput).ToList(), null);
            graph.MacroOutputNode = new K2Node_Tunnel(true, outgoingPins.Where(o => o.IsInput).ToList(), null);
            graph.MacroInputNode.CalculateSize();
            graph.MacroInputNode.SetPosition(-50 - graph.MacroInputNode.NodeWidth, 0);
            graph.MacroOutputNode.CalculateSize();
            graph.MacroOutputNode.SetPosition(maxX - minX + 50, 0);
            graph.AddNode(graph.MacroInputNode);
            graph.AddNode(graph.MacroOutputNode);
            
            return graph;
            
            // Find pins outside the macro that are used in the macro
            void CheckPin(GraphPin pin)
            {
                foreach (var pin2 in pin.LinkedTo)
                {
                    if (!nodes.Contains(pin2.ParentNode) && !outgoingPins.Contains(pin2))
                    {
                        pin2.LinkedTo.RemoveAll(o => o != pin);
                        outgoingPins.Add(pin2);
                    }
                }
            }
        }
    }
}
