using System;
using System.Collections.Generic;
using System.IO;

namespace mProjectLayout.Services;

public class SkeletonGenerator
{
    public (int directories, int files, List<string> log) Generate(string baseDir, string structure)
    {
        int dirCount = 0;
        int fileCount = 0;
        var log = new List<string>();

        var lines = structure.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Remove box-drawing characters
            line = line.Replace("│", "")
                       .Replace("├", "")
                       .Replace("└", "")
                       .Replace("─", "")
                       .Trim();
            line = line.Replace('/', Path.DirectorySeparatorChar);
            var path = Path.Combine(baseDir, line.TrimEnd('/'));

            if (line.EndsWith("/"))
            {
                if (Directory.Exists(path))
                {
                    log.Add($"Path exists {path}; continuing...");
                }
                else
                {
                    Directory.CreateDirectory(path);
                    dirCount++;
                    log.Add($"Created directory: {path}");
                }
            }
            else if (line.Contains('.'))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                if (File.Exists(path))
                {
                    log.Add($"File exists, skipping: {path}");
                }
                else
                {
                    File.Create(path).Dispose();
                    fileCount++;
                    log.Add($"Created file: {path}");
                }
            }
            else
            {
                log.Add($"Skipped unrecognized line: {line}");
            }
        }

        return (dirCount, fileCount, log);
    }
}