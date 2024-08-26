﻿using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using UnityDataTools.Analyzer;
using UnityDataTools.FileSystem;
using UnityDataTools.ReferenceFinder;
using UnityDataTools.TextDumper;

namespace UnityDataTools.UnityDataTool;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        UnityFileSystem.Init();

        var rootCommand = new RootCommand();

        {
            var pathArg = new Argument<DirectoryInfo>("path", "The path to the directory containing the files to analyze").ExistingOnly();
            var oOpt = new Option<string>(aliases: new[] { "--output-file", "-o" }, description: "Filename of the output database", getDefaultValue: () => "database.db");
            var sOpt = new Option<bool>(aliases: new[] { "--skip-references", "-s" }, description: "Skip CRC and do not extract references");
            var rOpt = new Option<bool>(aliases: new[] { "--extract-references", "-r" }) { IsHidden = true };
            var pOpt = new Option<string>(aliases: new[] { "--search-pattern", "-p" }, description: "File search pattern", getDefaultValue: () => "*");

            var analyzeCommand = new Command("analyze", "Analyze AssetBundles or SerializedFiles.")
            {
                pathArg,
                oOpt,
                sOpt,
                rOpt,
                pOpt,
            };

            analyzeCommand.AddAlias("analyse");
            analyzeCommand.SetHandler(
                (DirectoryInfo di, string o, bool s, bool r, string p) => Task.FromResult(HandleAnalyze(di, o, s, r, p)),
                pathArg, oOpt, sOpt, rOpt, pOpt);

            rootCommand.AddCommand(analyzeCommand);
        }

        {
            var pathArg = new Argument<FileInfo>("databasePath", "The path to the database generated by the 'analyze' command").ExistingOnly();
            var oOpt = new Option<string>(aliases: new[] { "--output-file", "-o" }, description: "Output file", getDefaultValue: () => "references.txt");
            var iOpt = new Option<long?>(aliases: new[] { "--object-id", "-i" }, description: "Object id ('id' column in the database)");
            var nOpt = new Option<string>(aliases: new[] { "--object-name", "-n" }, description: "Object name");
            var tOpt = new Option<string>(aliases: new[] { "--object-type", "-t" }, description: "Optional object type when searching by name");
            var aOpt = new Option<bool>(aliases: new[] { "--find-all", "-a" }, description: "Find all reference chains originating from the same asset (instead of only one), can be very slow");

            var findRefsCommand = new Command("find-refs", "Find reference chains to specified object(s).")
            {
                pathArg,
                oOpt,
                aOpt,
                nOpt,
                tOpt,
                iOpt,
            };

            findRefsCommand.SetHandler(
                (FileInfo fi, string o, long? i, string n, string t, bool a) => Task.FromResult(HandleFindReferences(fi, o, i, n, t, a)),
                pathArg, oOpt, iOpt, nOpt, tOpt, aOpt);

            rootCommand.Add(findRefsCommand);
        }

        {
            var pathArg = new Argument<FileInfo>("filename", "The path of the file to dump").ExistingOnly();
            var fOpt = new Option<DumpFormat>(aliases: new[] { "--output-format", "-f" }, description: "Output format", getDefaultValue: () => DumpFormat.Text);
            var sOpt = new Option<bool>(aliases: new[] { "--skip-large-arrays", "-s" }, description: "Do not dump large arrays of basic data types");
            var oOpt = new Option<DirectoryInfo>(aliases: new[] { "--output-path", "-o"}, description: "Output folder", getDefaultValue: () => new DirectoryInfo(Environment.CurrentDirectory));

            var dumpCommand = new Command("dump", "Dump the contents of an AssetBundle or SerializedFile.")
            {
                pathArg,
                fOpt,
                sOpt,
                oOpt,
            };
            dumpCommand.SetHandler(
                (FileInfo fi, DumpFormat f, bool s, DirectoryInfo o) => Task.FromResult(HandleDump(fi, f, s, o)),
                pathArg, fOpt, sOpt, oOpt);

            rootCommand.AddCommand(dumpCommand);
        }

        {
            var pathArg = new Argument<FileInfo>("filename", "The path of the archive file").ExistingOnly();
            var oOpt = new Option<DirectoryInfo>(aliases: new[] { "--output-path", "-o" }, description: "Output directory of the extracted archive", getDefaultValue: () => new DirectoryInfo("archive"));

            var extractArchiveCommand = new Command("extract", "Extract an AssetBundle or .data file.")
            {
                pathArg,
                oOpt,
            };

            extractArchiveCommand.SetHandler(
                (FileInfo fi, DirectoryInfo o) => Task.FromResult(Archive.HandleExtract(fi, o)),
                pathArg, oOpt);

            var listArchiveCommand = new Command("list", "List the contents of an AssetBundle or .data file.")
            {
                pathArg,
            };

            listArchiveCommand.SetHandler(
                (FileInfo fi) => Task.FromResult(Archive.HandleList(fi)),
                pathArg);

            var archiveCommand = new Command("archive", "Inspect or extract the contents of a Unity archive (AssetBundle or web platform .data file).")
            {
                extractArchiveCommand,
                listArchiveCommand,
            };

            rootCommand.AddCommand(archiveCommand);
        }

        var r = await rootCommand.InvokeAsync(args);

        UnityFileSystem.Cleanup();

        return r;
    }

    enum DumpFormat
    {
        Text,
    }

    static int HandleAnalyze(DirectoryInfo path, string outputFile, bool skipReferences, bool extractReferences, string searchPattern)
    {
        var analyzer = new AnalyzerTool();

        if (extractReferences)
        {
            Console.WriteLine("WARNING: --extract-references, -r option is deprecated (references are now extracted by default)");
        }

        return analyzer.Analyze(path.FullName, outputFile, searchPattern, skipReferences);
    }

    static int HandleFindReferences(FileInfo databasePath, string outputFile, long? objectId, string objectName, string objectType, bool findAll)
    {
        var finder = new ReferenceFinderTool();

        if ((objectId != null && objectName != null) || (objectId == null && objectName == null))
        {
            Console.Error.WriteLine("A value must be provided for either --object-id or --object-name.");
            return 1;
        }

        if (objectId != null)
        {
            return finder.FindReferences(objectId.Value, databasePath.FullName, outputFile, findAll);
        }
        else
        {
            return finder.FindReferences(objectName, objectType, databasePath.FullName, outputFile, findAll);
        }
    }

    static int HandleDump(FileInfo filename, DumpFormat format, bool skipLargeArrays, DirectoryInfo outputFolder)
    {
        switch (format)
        {
            case DumpFormat.Text:
            {
                var textDumper = new TextDumperTool();
                return textDumper.Dump(filename.FullName, outputFolder.FullName, skipLargeArrays);
            }
        }

        return 1;
    }
}
