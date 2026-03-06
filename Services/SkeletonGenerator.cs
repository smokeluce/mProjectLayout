using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace mProjectLayout.Services;

public class SkeletonGenerator
{
    private static readonly HashSet<string> KnownExtensionlessFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Makefile", "Dockerfile", "Jenkinsfile", "Procfile",
        "LICENSE", "LICENCE", "NOTICE", "AUTHORS", "CONTRIBUTORS",
        "CHANGELOG", "CHANGES", "HISTORY", "TODO", "COPYING",
        ".gitignore", ".gitattributes", ".editorconfig", ".env",
        ".dockerignore", ".npmignore", ".eslintignore"
    };

    private enum InputFormat { Tree, Path, Indented }

    private static InputFormat DetectFormat(string[] lines)
    {
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.Contains('├') || line.Contains('└') || line.Contains('│'))
                return InputFormat.Tree;
            var trimmed = line.Trim();
            if (trimmed.Contains('\\') ||
                (trimmed.Contains('/') && !trimmed.EndsWith('/')))
                return InputFormat.Path;
        }
        return InputFormat.Indented;
    }

    public (int directories, int files, List<string> log) Generate(string baseDir, string structure)
    {
        var log   = new List<string>();
        var lines = structure.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var format = DetectFormat(lines);
        log.Add($"🔍 Detected input format: {format}");

        return format switch
        {
            InputFormat.Tree     => ProcessTree(baseDir, lines, log),
            InputFormat.Path     => ProcessPaths(baseDir, lines, log),
            InputFormat.Indented => ProcessIndented(baseDir, lines, log),
            _                    => (0, 0, log)
        };
    }

    // =========================================================================
    // Format 1 — Tree (box-drawing characters)
    // =========================================================================
    private (int, int, List<string>) ProcessTree(string baseDir, string[] lines, List<string> log)
    {
        int dirCount = 0, fileCount = 0;

        // depthStack[d] = directory name at depth d currently open
        var depthStack = new List<string>();

        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;

            // Measure depth BEFORE stripping — box chars encode the nesting level.
            // A line with NO leading box chars (e.g. "project/") is the root → depth 0.
            // A line starting with ├── or └── (no leading │) is depth 1.
            // A line with one │ before ├──/└── is depth 2, etc.
            int depth = MeasureTreeDepth(raw);

            string name = StripTreeChars(raw);
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (name.StartsWith('#')) { log.Add($"💬 Skipped comment: {name}"); continue; }

            bool hadTrailingSlash = name.EndsWith('/') || name.EndsWith('\\');
            string cleanName = name.TrimEnd('/', '\\');
            bool isDir = !IsFile(cleanName, hadTrailingSlash);

            // Trim stack to parent depth (depth - 1 for files, depth for dirs)
            // Stack holds the open directory chain; we want exactly `depth` entries
            // representing the ancestors of this entry.
            while (depthStack.Count > depth)
                depthStack.RemoveAt(depthStack.Count - 1);

            // Build full path: baseDir + ancestors + this name
            var parts = new List<string> { baseDir };
            parts.AddRange(depthStack);
            parts.Add(cleanName);
            string fullPath = Path.Combine(parts.ToArray());

            if (isDir)
            {
                // Push this directory so children can reference it
                depthStack.Add(cleanName);
                CreateDirectory(fullPath, log, ref dirCount);
            }
            else
            {
                EnsureParent(fullPath);
                CreateFile(fullPath, log, ref fileCount);
            }
        }

        return (dirCount, fileCount, log);
    }

    // =========================================================================
    // Measures depth of a tree line.
    //
    // Standard tree indent anatomy (each level = 4 chars):
    //   root line        → no leading chars          → depth 0
    //   "├── name"       → branch with no pipe        → depth 1
    //   "│   ├── name"   → one pipe + branch          → depth 2
    //   "│   │   └── name" → two pipes + branch       → depth 3
    //
    // We count leading pipe/space blocks, then the branch char puts us at depth+1.
    // =========================================================================
    private static int MeasureTreeDepth(string raw)
    {
        int i = 0;
        int pipeCount = 0;

        while (i < raw.Length)
        {
            // Pipe block: │ + 3 spaces (or just │ at end)
            if (raw[i] == '│')
            {
                pipeCount++;
                i += 4;
                continue;
            }

            // 4-space block (open indent, no pipe)
            if (i + 3 < raw.Length &&
                raw[i]   == ' ' && raw[i+1] == ' ' &&
                raw[i+2] == ' ' && raw[i+3] == ' ')
            {
                pipeCount++;
                i += 4;
                continue;
            }

            // Branch character — this entry is one level deeper than pipe count
            if (raw[i] == '├' || raw[i] == '└')
                return pipeCount + 1;

            // No leading chars at all — this is the root entry, depth 0
            return 0;
        }

        return 0;
    }

    // =========================================================================
    // Format 2 — Explicit paths
    // =========================================================================
    private (int, int, List<string>) ProcessPaths(string baseDir, string[] lines, List<string> log)
    {
        int dirCount = 0, fileCount = 0;

        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            if (trimmed.StartsWith('#')) { log.Add($"💬 Skipped comment: {trimmed}"); continue; }

            trimmed = trimmed.Replace('/', Path.DirectorySeparatorChar)
                             .Replace('\\', Path.DirectorySeparatorChar);

            var segments = trimmed.Split(Path.DirectorySeparatorChar,
                                         StringSplitOptions.RemoveEmptyEntries);

            string fullPath = Path.Combine(new[] { baseDir }.Concat(segments).ToArray());

            bool hasTrailingSlash = raw.TrimEnd().EndsWith('/') || raw.TrimEnd().EndsWith('\\');
            string lastName = segments[^1];
            bool isDir = !IsFile(lastName, hasTrailingSlash);

            if (isDir)
                CreateDirectory(fullPath, log, ref dirCount);
            else
            {
                EnsureParent(fullPath);
                CreateFile(fullPath, log, ref fileCount);
            }
        }

        return (dirCount, fileCount, log);
    }

    // =========================================================================
    // Format 3 — Indented (spaces only)
    // =========================================================================
    private (int, int, List<string>) ProcessIndented(string baseDir, string[] lines, List<string> log)
    {
        int dirCount = 0, fileCount = 0;
        var depthStack = new List<string>();

        int indentUnit = DetectIndentUnit(lines);
        log.Add($"↔  Indent unit detected: {indentUnit} spaces");

        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var trimmed = raw.TrimEnd();
            string name = trimmed.TrimStart();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (name.StartsWith('#')) { log.Add($"💬 Skipped comment: {name}"); continue; }

            int leadingSpaces = trimmed.Length - name.Length;
            int depth = indentUnit > 0 ? leadingSpaces / indentUnit : 0;

            while (depthStack.Count > depth)
                depthStack.RemoveAt(depthStack.Count - 1);

            bool hadTrailingSlash = name.EndsWith('/') || name.EndsWith('\\');
            string cleanName = name.TrimEnd('/', '\\');
            bool isDir = !IsFile(cleanName, hadTrailingSlash);

            var parts = new List<string> { baseDir };
            parts.AddRange(depthStack);
            parts.Add(cleanName);
            string fullPath = Path.Combine(parts.ToArray());

            if (isDir)
            {
                depthStack.Add(cleanName);
                CreateDirectory(fullPath, log, ref dirCount);
            }
            else
            {
                EnsureParent(fullPath);
                CreateFile(fullPath, log, ref fileCount);
            }
        }

        return (dirCount, fileCount, log);
    }

    // =========================================================================
    // Shared helpers
    // =========================================================================

    private static string StripTreeChars(string raw)
    {
        string s = Regex.Replace(raw, @"[│├└─]", "");
        return s.Replace("|", "").Replace("+", "").Trim();
    }

    private static int DetectIndentUnit(string[] lines)
    {
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            int spaces = line.Length - line.TrimStart().Length;
            if (spaces > 0) return spaces;
        }
        return 4;
    }

    private static bool IsFile(string name, bool hadTrailingSlash)
    {
        if (hadTrailingSlash) return false;
        if (KnownExtensionlessFiles.Contains(name)) return true;
        if (name.StartsWith('.') && name.Length > 1 && !name[1..].Contains('.')) return true;
        return Path.GetExtension(name).Length > 0;
    }

    private static void EnsureParent(string fullPath)
    {
        string? parent = Path.GetDirectoryName(fullPath);
        if (parent != null && !Directory.Exists(parent))
            Directory.CreateDirectory(parent);
    }

    private static void CreateDirectory(string path, List<string> log, ref int count)
    {
        if (Directory.Exists(path))
            log.Add($"⚠  Directory exists, skipping: {path}");
        else
        {
            Directory.CreateDirectory(path);
            count++;
            log.Add($"📁 Created directory: {path}");
        }
    }

    private static void CreateFile(string path, List<string> log, ref int count)
    {
        if (File.Exists(path))
            log.Add($"⚠  File exists, skipping: {path}");
        else
        {
            File.Create(path).Dispose();
            count++;
            log.Add($"📄 Created file: {path}");
        }
    }
}
