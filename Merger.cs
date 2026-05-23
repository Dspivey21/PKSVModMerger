using System;
using System.Collections.Generic;
using System.Linq;

namespace PKSVModMerger;

internal static class Merger
{
    public sealed record Result(int FinalActive, int FinalUnused, string OutputPath);

    public static Result Run(string basePath, IReadOnlyList<string> addPaths, string outputPath, Action<string> log)
    {
        log($"[base ] {basePath}");
        var merged = FlatIo.Load(basePath);
        log($"  FileHashes={merged.FileHashes.Count}, UnusedHashes={merged.UnusedHashes.Count}");

        var active = new Dictionary<ulong, FdEntry>(merged.FileHashes.Count);
        for (int i = 0; i < merged.FileHashes.Count; i++)
            active[merged.FileHashes[i]] = merged.FileInfo[i];

        var unused = new Dictionary<ulong, FdEntry>(merged.UnusedHashes.Count);
        for (int i = 0; i < merged.UnusedHashes.Count; i++)
            unused[merged.UnusedHashes[i]] = merged.UnusedFileInfo[i];

        var packNamesSet = new HashSet<string>(merged.PackNames, StringComparer.Ordinal);

        foreach (var addPath in addPaths)
        {
            log($"[add  ] {addPath}");
            var add = FlatIo.Load(addPath);
            log($"  FileHashes={add.FileHashes.Count}, UnusedHashes={add.UnusedHashes.Count}, PackNames={add.PackNames.Count}");

            int moved = 0, alreadyUnused = 0, appended = 0;
            for (int j = 0; j < add.UnusedHashes.Count; j++)
            {
                ulong h = add.UnusedHashes[j];

                if (unused.ContainsKey(h))
                {
                    alreadyUnused++;
                    continue;
                }

                if (active.TryGetValue(h, out var fi))
                {
                    active.Remove(h);
                    unused[h] = fi;
                    moved++;
                }
                else
                {
                    unused[h] = add.UnusedFileInfo[j];
                    appended++;
                }
            }

            int newPacks = 0;
            foreach (var pn in add.PackNames)
            {
                if (packNamesSet.Add(pn))
                {
                    merged.PackNames.Add(pn);
                    merged.PackInfo.Add(new FdPack());
                    newPacks++;
                }
            }

            log($"  moved={moved}, alreadyUnused={alreadyUnused}, appended={appended}, newPackNames={newPacks}");
        }

        var activeSorted = active.OrderBy(kv => kv.Key).ToList();
        merged.FileHashes = activeSorted.Select(kv => kv.Key).ToList();
        merged.FileInfo = activeSorted.Select(kv => kv.Value).ToList();

        var unusedSorted = unused.OrderBy(kv => kv.Key).ToList();
        merged.UnusedHashes = unusedSorted.Select(kv => kv.Key).ToList();
        merged.UnusedFileInfo = unusedSorted.Select(kv => kv.Value).ToList();

        log($"[final] FileHashes={merged.FileHashes.Count}, UnusedHashes={merged.UnusedHashes.Count}");

        FlatIo.Save(merged, outputPath);
        log($"[save ] {outputPath}");

        return new Result(merged.FileHashes.Count, merged.UnusedHashes.Count, outputPath);
    }
}
