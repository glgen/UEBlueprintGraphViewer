using CUE4Parse.UE4.Kismet;
using System;
using System.Collections.Generic;
using System.Linq;
using UEBlueprintGraphViewer.ControlFlow;
using static UEBlueprintGraphViewer.Engine.Utils;
using static UEBlueprintGraphViewer.Engine.PropertiesUtils;

namespace UEBlueprintGraphViewer.Decompiler
{
    public enum BlockType
    {
        Unknown,
        Jump,
        BranchEnd,
        BranchEndIfNot,
        LatentAction,
        Return,
        JumpIfNot,
        JumpToEntryPoint,
    }

    public class InstrBlock
    {
        public readonly List<KismetExpression> Instructions = [];
        public readonly List<BlockJump> Jumps = [];
        public readonly List<Sequence> Sequences = [];

        public BlockType Type;

        public int Start;
        public int End;

        public void Add(KismetExpression expr)
        {
            if (Instructions.Count == 0)
                Start = expr.StatementIndex;

            End = expr.StatementIndex;

            Instructions.Add(expr);
        }

        public override string ToString()
        {
            return $"{Start} - {End}";
        }

        public bool IsInside(uint index)
        {
            return index >= Start & index <= End;
        }

        public int GetIndex(uint statementIndex)
        {
            return Instructions.FindIndex(o => o.StatementIndex == statementIndex);
        }

        public Sequence GetSequence(int localIndex)
        {
            return Sequences.Find(o => o.SourceIndex <= localIndex && o.EndIndex >= localIndex)
                ?? throw new DecompilerException($"Sequence with index {localIndex} not found");
        }
    }

    public class BlockJump
    {
        public InstrBlock Source;
        public InstrBlock Destination;
        public int SourceIndex;
        public int StartIndex;

        public BlockJump(InstrBlock source, InstrBlock destination)
        {
            Source = source;
            Destination = destination;
            SourceIndex = source.Instructions.Count - 1;
        }

        public BlockJump(InstrBlock source, InstrBlock destination, uint statementIndex) : this(source, destination)
        {
            StartIndex = destination.GetIndex(statementIndex);
        }

        public override string ToString()
        {
            return $"({Source}) -> ({Destination}) at {StartIndex}";
        }

        public KismetExpression GetDestination()
        {
            return Destination.Instructions[StartIndex];
        }
    }

    public class Sequence(List<BlockJump> points, int start, int end)
    {
        public List<BlockJump> Points = points;
        public int SourceIndex = start;
        public int EndIndex = end;

        public bool HaveThisBranchStart(InstrBlock block, int index)
        {
            return Points.Exists(o => o.Destination == block && o.StartIndex == index);
        }

        public List<BlockJump> GetPoints(int localIndex)
        {
            int start = localIndex - SourceIndex;
            return Points.GetRange(start, Points.Count - start);
        }

        public IEnumerable<uint> GetPointsStatementIndexes()
        {
            return Points.Select(o => (uint)o.GetDestination().StatementIndex);
        }
    }

    public class ExecutionFlow
    {
        public List<InstrBlock> Blocks = [];
        public List<uint> EntryPoints = [];
        private Dictionary<uint, BlockJump> EntryPointJumps = [];

        // Decompiling kismet bytecodes into nodes
        public void DecompileFlow(IEnumerable<KismetExpression> script)
        {
            FindBlocks(script);
            FindSequences();
            FindBasicJumps();
            ResolveEntryPointJumps();
        }

        private void FindBlocks(IEnumerable<KismetExpression> script)
        {
            InstrBlock block = new();
            foreach (KismetExpression expr in script)
            {
                block.Add(expr);

                BlockType? type = expr switch
                {
                    EX_JumpIfNot => BlockType.JumpIfNot,
                    EX_Jump => BlockType.Jump,
                    EX_PopExecutionFlow => BlockType.BranchEnd,
                    EX_PopExecutionFlowIfNot => BlockType.BranchEndIfNot,
                    EX_EndOfScript => BlockType.Return,
                    EX_ComputedJump => BlockType.JumpToEntryPoint,
                    _ => null,
                };

                if (type != null)
                {
                    block.Type = type.Value;
                    Blocks.Add(block);
                    block = new();
                }
            }
        }

        private void FindSequences()
        {
            foreach (var b in Blocks)
            {
                for (int i = 0; i < b.Instructions.Count - 1; i++)
                {
                    List<BlockJump> seq = [];

                    int count = 0;
                    while (b.Instructions[i + count] is EX_PushExecutionFlow push)
                    {
                        BlockJump jmp = FindBlockJump(push.PushingAddress, b);
                        jmp.SourceIndex = i;
                        seq.Add(jmp);
                        count++;
                    }

                    if (count > 0)
                    {
                        BlockJump jmp = FindBlockJump((uint)b.Instructions[i + count].StatementIndex, b);
                        jmp.SourceIndex = i;
                        seq.Add(jmp);
                        Sequence sequence = new(seq, i, i + (count - 1));
                        b.Sequences.Add(sequence);
                        i += count - 1;
                    }
                }
            }
        }

        private void FindBasicJumps()
        {
            for (int i = 0; i < Blocks.Count; i++)
            {
                var b = Blocks[i];

                // latent action nodes (async nodes)
                if (b.Instructions.Count >= 2)
                {
                    if (IsLatentFunc(b.Instructions[^2], out int? offset))
                    {
                        b.Type = BlockType.LatentAction;
                        if (offset == -1) continue;
                        b.Jumps.Add(FindBlockJump((uint)offset!, b));
                        continue;
                    }
                }

                switch (b.Type)
                {
                    case BlockType.JumpIfNot:
                        b.Jumps.Add(new(b, Blocks[i + 1]));
                        b.Jumps.Add(FindBlockJump((b.Instructions.Last() as EX_JumpIfNot)!.CodeOffset, b));
                        break;
                    case BlockType.BranchEndIfNot:
                        b.Jumps.Add(new(b, Blocks[i + 1]));
                        break;
                    case BlockType.Jump:
                        b.Jumps.Add(FindBlockJump((b.Instructions.Last() as EX_Jump)!.CodeOffset, b));
                        break;
                }
            }
        }

        private void ResolveEntryPointJumps()
        {
            foreach (uint point in EntryPoints)
            {
                EntryPointJumps.Add(point, FindBlockJump(point, Blocks.First()));
            }
        }
        
        public BlockJump GetEntryPointJump(uint point) => EntryPointJumps[point];
        
        private BlockJump FindBlockJump(uint statementIndex, InstrBlock source)
        {
            var a = Blocks.Find(o => o.IsInside(statementIndex));
            return new(source, a, statementIndex);
        }

        public (InstrBlock, int) FindInBlocks(uint statementIndex)
        {
            var startBlock = Blocks.Find(o => o.IsInside(statementIndex));
            var startIndex = startBlock.GetIndex(statementIndex);
            return (startBlock, startIndex);
        }

    }
}
