﻿using Cassette.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Storage = System.IO.IsolatedStorage;

namespace Cassette.Stylesheets
{
    public class LessJsNodeCompiler : ILessJsNodeCompiler
    {
        private CompileResult compileResult;
        private Exception compileException;

        private string createdDirectory;
        private string applicationRootDirectory;
        public CompileResult Compile(string source, CompileContext context)
        {
            var sourceFile = context.RootDirectory.GetFile(context.SourceFilePath);
            sourceFile = EnsureExists(source, sourceFile, context.RootDirectory);
            NormalizeSourceFile(sourceFile, source);
            try {
                var rootDirectory = sourceFile.Directory.GetDirectory("~/");
                applicationRootDirectory = (string)rootDirectory.GetType().GetField("fullSystemPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.GetField | System.Reflection.BindingFlags.Instance).GetValue(rootDirectory);
                applicationRootDirectory = string.Format("{0}{1}", applicationRootDirectory[0].ToString().ToLowerInvariant(), applicationRootDirectory.Substring(1));
            }
            catch { }
            return Compile(sourceFile);
        }

        private void NormalizeSourceFile(IFile sourceFile, string source)
        {
            // Strictly to overcome limitations in unit testing where the file is empty but
            // the source is filled in.  We'll compare file contents to passed in source and 
            // overwrite the file if the two don't match.  What a PITA.
            string fileSource;
            using (var reader = new StreamReader(sourceFile.OpenRead())) {
                fileSource = reader.ReadToEnd();
                reader.Close();
            }
            if (fileSource != source) {
                using (var writer = new StreamWriter(sourceFile.Open(FileMode.Open, FileAccess.Write, FileShare.None))) {
                    writer.Write(source);
                    writer.Flush();
                    writer.Close();
                }
            }
        }

        private IFile EnsureExists(string source, IFile sourceFile, IDirectory rootDirectory)
        {
            if (sourceFile.Exists && sourceFile is FileSystemFile)
                return sourceFile;

            var destinationDirectoryInfo = CreateDirectory("~/");
            IFile excludeFile = null;
            if (!sourceFile.Exists) {
                using (var reader = sourceFile.OpenRead()) {
                    if (reader == null) {
                        Copy(sourceFile, destinationDirectoryInfo.Item1, () => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(source)));
                        excludeFile = sourceFile;
                    }
                }
            }
            // Marshal to something on the filesystem - node.exe depends on it!
            Marshal(rootDirectory, destinationDirectoryInfo.Item1, excludeFile);
            return destinationDirectoryInfo.Item1.GetFile(sourceFile.FullPath);
        }

        private void Marshal(IDirectory source, IDirectory destination, IFile excludeFileFromCopy)
        {
            //Copy(sourceFile, destination, sourceFile.OpenRead);
            foreach (var sourceItem in source.GetFiles("*.*", SearchOption.AllDirectories).Where(f => f != excludeFileFromCopy)) {
                Copy(sourceItem, destination, sourceItem.OpenRead);
            }
        }

        private void Copy(IFile sourceFile, IDirectory directory, Func<Stream> sourceReader)
        {
            var destFile = directory.GetFile(sourceFile.FullPath);

            if (!destFile.Directory.Exists) destFile.Directory.Create();
            using (var reader = sourceReader())
            using (var destStream = destFile.Open(FileMode.OpenOrCreate, FileAccess.Write, FileShare.None)) {
                if (reader == null) return; // e.g. this is a mock file
                reader.CopyTo(destStream);
                destStream.Flush();
                destStream.Close();
                reader.Close();
            }
        }

        private Tuple<IDirectory, string> CreateDirectory(string fullPath)
        {
            var basePath = GetTempFolder();
            var combinedPath = Path.Combine(basePath, fullPath.Substring(2));
            return Tuple.Create((IDirectory)new FileSystemDirectory(combinedPath), combinedPath);
        }

        private string GetTempFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            while (Directory.Exists(folder) || File.Exists(folder)) {
                folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            }
            System.IO.Directory.CreateDirectory(folder);
            createdDirectory = folder;
            return folder;
        }

        private CompileResult Compile(IFile source)
        {
            string output = Path.GetTempFileName();
            // Assumes source is a Cassette.IO.FileSystemFile
            string absolutePath = (string)source.GetType().GetField("systemAbsoluteFilename", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.GetField | System.Reflection.BindingFlags.Instance).GetValue(source);
            string arguments = String.Format("less\\bin\\lessc --no-color --relative-urls \"{0}\" \"{1}\"", absolutePath, output);
            string tempPath = GetExecutablePath();

            ProcessStartInfo start = new ProcessStartInfo(Path.Combine(tempPath, "node.exe")){
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WorkingDirectory = tempPath
            };
            start.EnvironmentVariables["output"] = output;
            start.EnvironmentVariables["fileName"] = source.FullPath;
            Process p = new Process();
            p.StartInfo = start;
            p.EnableRaisingEvents = true;
            p.Exited += ProcessExited;
            p.Start();

            const int SLEEP_AMOUNT = 20;
            var elapsedTime = new TimeSpan(0);
            while (compileResult == null && compileException == null && (elapsedTime = elapsedTime.Add(new TimeSpan(0, 0, 0, 0, SLEEP_AMOUNT))).TotalSeconds <= 30)
                System.Threading.Thread.Sleep(SLEEP_AMOUNT);
            if (compileException != null)
                throw compileException;
            if (compileResult == null)
                throw new TimeoutException("node failed to compile and exit in a timely manner");

            return compileResult;
        }

        private void ProcessExited(object sender, EventArgs e)
        {
            using (Process process = (Process)sender) {
                string fileName = process.StartInfo.EnvironmentVariables["fileName"];

                try {
                    compileResult = ProcessResult(process);
                }
                catch (Exception ex) {
                    compileException = ex;
                }

                process.Exited -= ProcessExited;
            }
            try {
                if (createdDirectory != null)
                    System.IO.Directory.Delete(createdDirectory, true);
            }
            catch {

            }
        }

        private CompileResult ProcessResult(Process process)
        {
            string output = process.StartInfo.EnvironmentVariables["output"];
            string result = null;
            IEnumerable<string> paths = Enumerable.Empty<string>();
            if (File.Exists(output)) {
                result = File.ReadAllText(output);
                File.Delete(output);

                if (process.ExitCode == 0) {
                    using (var reader = process.StandardOutput) {
                        paths = reader.ReadToEnd().Split('\n')
                                    .Where(s => !string.IsNullOrWhiteSpace(s))
                                    .Select(s => s[0].ToString().ToLowerInvariant() + s.Substring(1))
                                    .Select(s => s.Replace("\r", "").Replace("\\", "/").Replace(applicationRootDirectory ?? "~", "~"));
                    }
                } else {
                    using (StreamReader reader = process.StandardError) {
                        var message = ParseError(reader.ReadToEnd()).ToString();
                        if (!string.IsNullOrWhiteSpace(applicationRootDirectory)) {
                            message = System.Text.RegularExpressions.Regex.Replace(message, applicationRootDirectory.Replace("/", "[/\\\\]"), "~", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        }
                        throw new LessJsNodeCompileException(message.Replace('`', '\''));
                    }
                }
            }
            return new CompileResult(result, paths);
        }

        private CompilerError ParseError(string error)
        {
            CompilerError result = new CompilerError();
            string[] lines = error.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i];

                if (error.Contains("message:")) {
                    string[] args = line.Split(new[] { ':' }, 2);

                    if (args[0].Trim() == "message")
                        result.Message = args[1].Trim();

                    if (args[0].Trim() == "filename")
                        result.FileName = args[1].Trim();

                    int lineNo = 0;
                    if (args[0].Trim() == "line" && int.TryParse(args[1], out lineNo))
                        result.Line = lineNo;

                    int columnNo = 0;
                    if (args[0].Trim() == "column" && int.TryParse(args[1], out columnNo))
                        result.Column = columnNo;
                } else {
                    // I believe Less 1.5 changed their error messaging and Windows Essentials hasn't kept up
                    // We'll detect a 1.5 style error message and return the result immediately
                    
                    // ParseError: Unrecognised input in C:\Users\Emil\AppData\Local\Temp\2b354488-7d3e-40dd-ba51-41a23d152281\test.less on line 1, column 9:
                    if (i == 0 && System.Text.RegularExpressions.Regex.IsMatch(line, @"^[A-Z][a-z]+Error: ")) {
                        result.Message = line;
                        var args = line.Split(',');
                        if (args.Length > 1 && args[1].StartsWith(" column")) {
                            var column = args[1].Substring(" column".Length).TrimEnd(':');
                            int columnNo;
                            if (int.TryParse(column, out columnNo))
                                result.Column = columnNo;
                        }
                        if (args.Length > 0) {
                            var matches = System.Text.RegularExpressions.Regex.Match(args[0], @"line (\d+)$");
                            if (matches.Success && matches.Groups.Count > 1) {
                                var lineStr = matches.Groups[1].Value;
                                int lineNo;
                                if (int.TryParse(lineStr, out lineNo))
                                    result.Line = lineNo;
                            }
                        }
                        var fileNameMatch = System.Text.RegularExpressions.Regex.Match(line, @" in ([a-zA-Z\\:.0-9\-/]+) on line \d+, column \d+:$");
                        if (fileNameMatch.Success && fileNameMatch.Groups.Count > 1) {
                            result.FileName = fileNameMatch.Groups[1].Value.Replace('\\', '/');
                            result.Message = line.Substring(0, fileNameMatch.Groups[0].Index);
                        }
                        return result;
                    }
                    if (i == 1 || i == 2)
                        result.Message += " " + line;

                    if (i == 3) {
                        string[] lineCol = line.Split(',');

                        int lineNo = 0;
                        if (int.TryParse(lineCol[0].Replace("on line", string.Empty).Trim(), out lineNo))
                            result.Line = lineNo;

                        int columnNo = 0;
                        if (int.TryParse(lineCol[0].Replace("column", string.Empty).Trim(':').Trim(), out columnNo))
                            result.Column = columnNo;

                        result.Message = result.Message.Trim();
                    }

                }
            }

            return result;
        }

        private static string GetExecutablePath()
        {
            string tempPath = System.IO.Path.GetTempPath();
            string nodejs = tempPath + "node.exe";
            string less = tempPath + "npm_less.zip";

            CreateIfNotExists(new Dictionary<string, string>{
                {less, "npm_less"},
                {nodejs, "node"},
            });

            return tempPath;
        }

        private static void CreateIfNotExists(IEnumerable<KeyValuePair<string, string>> files)
        {
            foreach (var kvp in files)
                CreateIfNotExists(kvp.Key, kvp.Value);
        }

        private static void CreateIfNotExists(string path, string resourceKey)
        {
            if (File.Exists(path)) {
                assemblyDate = assemblyDate ?? RetrieveLinkerTimestamp(typeof(LessJsNodeCompiler).Assembly.Location);
                if (!assemblyDate.HasValue) return;
                var utcAssemblyDate = TimeZone.CurrentTimeZone.ToUniversalTime(assemblyDate.Value);
                if (File.GetLastWriteTimeUtc(path) > utcAssemblyDate) return;
            }            
            File.WriteAllBytes(path, (byte[])Resources.ResourceManager.GetObject(resourceKey));
            if (resourceKey == "npm_less") {
                using (var zip = Ionic.Zip.ZipFile.Read(path)) {
                    zip.ExtractAll(System.IO.Path.GetDirectoryName(path), Ionic.Zip.ExtractExistingFileAction.OverwriteSilently);
                }
            }
        }

        private static DateTime? assemblyDate;
        /// <summary>
        /// Retrieves the date/time that the assembly was linked
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>Date/time the assembly was linked, or null if an error occurred</returns>
        private static DateTime? RetrieveLinkerTimestamp(string filePath)
        {
            const int peHeaderOffset = 60;
            const int linkerTimestampOffset = 8;

            var b = new byte[2048];
            try {
                using (var s = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    s.Read(b, 0, 2048);
            }
            catch (Exception ex) {
                Debug.WriteLine("Error reading assembly file to get linker timestamp.  Message: " + ex);
                return null;
            }

            var i = BitConverter.ToInt32(b, peHeaderOffset);

            var secondsSince1970 = BitConverter.ToInt32(b, i + linkerTimestampOffset);
            var dt = TimeZone
                        .CurrentTimeZone
                        .ToLocalTime(
                            new DateTime(1970, 1, 1, 0, 0, 0).AddSeconds(secondsSince1970)
                            );
            return dt;
        }

        private class CompilerError
        {
            public int Line { get; set; }
            public int Column { get; set; }
            public string FileName { get; set; }
            public string Message { get; set; }

            public override string ToString()
            {
                if (Column != 0)
                    return Message + " on line " + Line + ", column " + Column + " in file '" + FileName + "'";
                return Message + " on line " + Line + " in file '" + FileName + "'";
            }
        }
    }
}
