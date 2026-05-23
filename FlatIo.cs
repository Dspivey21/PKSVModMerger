using System.IO;
using Google.FlatBuffers;
using PKSVModMerger.Schema;
using FbFileInfo = PKSVModMerger.Schema.FileInfo;

namespace PKSVModMerger;

// Translates between the on-disk FlatBuffer binary and our mutable FdData.
// Load: copy values out of the read-only FlatBuffer accessor into POCOs.
// Save: hand POCO contents to a fresh FlatBufferBuilder.

internal static class FlatIo
{
    public static FdData Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var fb = FileDescriptor.GetRootAsFileDescriptor(new ByteBuffer(bytes));
        var data = new FdData();

        for (int i = 0; i < fb.FileHashesLength; i++)
            data.FileHashes.Add(fb.FileHashes(i));

        for (int i = 0; i < fb.PackNamesLength; i++)
            data.PackNames.Add(fb.PackNames(i));

        for (int i = 0; i < fb.FileInfoLength; i++)
        {
            var fi = fb.FileInfo(i)!.Value;
            data.FileInfo.Add(new FdEntry { PackIndex = fi.PackIndex, UnusedTable = fi.UnusedTable });
        }

        for (int i = 0; i < fb.PackInfoLength; i++)
        {
            var pi = fb.PackInfo(i)!.Value;
            data.PackInfo.Add(new FdPack { FileSize = pi.FileSize, FileCount = pi.FileCount });
        }

        for (int i = 0; i < fb.UnusedHashesLength; i++)
            data.UnusedHashes.Add(fb.UnusedHashes(i));

        for (int i = 0; i < fb.UnusedFileInfoLength; i++)
        {
            var fi = fb.UnusedFileInfo(i)!.Value;
            data.UnusedFileInfo.Add(new FdEntry { PackIndex = fi.PackIndex, UnusedTable = fi.UnusedTable });
        }

        NormalizeVectors(data);
        return data;
    }

    public static void Save(FdData data, string path)
    {
        var builder = new FlatBufferBuilder(1 << 20);

        var fileHashesVec = FileDescriptor.CreateFileHashesVector(builder, data.FileHashes.ToArray());

        var packNameOffsets = new StringOffset[data.PackNames.Count];
        for (int i = 0; i < data.PackNames.Count; i++)
            packNameOffsets[i] = builder.CreateString(data.PackNames[i]);
        var packNamesVec = FileDescriptor.CreatePackNamesVector(builder, packNameOffsets);

        var fileInfoOffsets = new Offset<FbFileInfo>[data.FileInfo.Count];
        for (int i = 0; i < data.FileInfo.Count; i++)
            fileInfoOffsets[i] = FbFileInfo.CreateFileInfo(builder, data.FileInfo[i].PackIndex, data.FileInfo[i].UnusedTable);
        var fileInfoVec = FileDescriptor.CreateFileInfoVector(builder, fileInfoOffsets);

        var packInfoOffsets = new Offset<PackInfo>[data.PackInfo.Count];
        for (int i = 0; i < data.PackInfo.Count; i++)
            packInfoOffsets[i] = PackInfo.CreatePackInfo(builder, data.PackInfo[i].FileSize, data.PackInfo[i].FileCount);
        var packInfoVec = FileDescriptor.CreatePackInfoVector(builder, packInfoOffsets);

        var unusedHashesVec = FileDescriptor.CreateUnusedHashesVector(builder, data.UnusedHashes.ToArray());

        var unusedFileInfoOffsets = new Offset<FbFileInfo>[data.UnusedFileInfo.Count];
        for (int i = 0; i < data.UnusedFileInfo.Count; i++)
            unusedFileInfoOffsets[i] = FbFileInfo.CreateFileInfo(builder, data.UnusedFileInfo[i].PackIndex, data.UnusedFileInfo[i].UnusedTable);
        var unusedFileInfoVec = FileDescriptor.CreateUnusedFileInfoVector(builder, unusedFileInfoOffsets);

        var root = FileDescriptor.CreateFileDescriptor(builder,
            fileHashesVec, packNamesVec, fileInfoVec, packInfoVec, unusedHashesVec, unusedFileInfoVec);

        builder.Finish(root.Value);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, builder.SizedByteArray());
    }

    // Some stock TRPFDs ship with FileInfo vectors that don't line up 1:1 with their
    // hash vectors. Pad with defaults or truncate so the parallel arrays stay aligned.
    private static void NormalizeVectors(FdData data)
    {
        while (data.FileInfo.Count < data.FileHashes.Count) data.FileInfo.Add(new FdEntry());
        while (data.FileInfo.Count > data.FileHashes.Count) data.FileInfo.RemoveAt(data.FileInfo.Count - 1);

        while (data.UnusedFileInfo.Count < data.UnusedHashes.Count) data.UnusedFileInfo.Add(new FdEntry());
        while (data.UnusedFileInfo.Count > data.UnusedHashes.Count) data.UnusedFileInfo.RemoveAt(data.UnusedFileInfo.Count - 1);
    }
}
