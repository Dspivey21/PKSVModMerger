using System.Collections.Generic;

namespace PKSVModMerger;

// In-memory representation of a TRPFD. Plain mutable containers — the merge
// algorithm builds these up and FlatIo translates to/from the FlatBuffer
// binary at the edges.

internal sealed class FdEntry
{
    public ulong PackIndex;
    public uint UnusedTable;
}

internal sealed class FdPack
{
    public ulong FileSize;
    public ulong FileCount;
}

internal sealed class FdData
{
    public List<ulong> FileHashes { get; set; } = new();
    public List<string> PackNames { get; set; } = new();
    public List<FdEntry> FileInfo { get; set; } = new();
    public List<FdPack> PackInfo { get; set; } = new();
    public List<ulong> UnusedHashes { get; set; } = new();
    public List<FdEntry> UnusedFileInfo { get; set; } = new();
}
