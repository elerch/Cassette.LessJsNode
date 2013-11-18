using Cassette.IO;
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

            ProcessStartInfo start = new ProcessStartInfo("node.exe"){
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
                        if (!string.IsNullOrWhiteSpace(applicationRootDirectory))
                            message = message.Replace(applicationRootDirectory, "~");
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
            if (File.Exists(path)) return;
            File.WriteAllBytes(path, (byte[])Resources.ResourceManager.GetObject(resourceKey));
            if (resourceKey == "npm_less") {
                using (var zip = Ionic.Zip.ZipFile.Read(path)) {
                    zip.ExtractAll(System.IO.Path.GetDirectoryName(path));
                }
            }
        }

        private class CompilerError
        {
            public int Line { get; set; }
            public int Column { get; set; }
            public string FileName { get; set; }
            public string Message { get; set; }

            public override string ToString()
            {
                return Message + " on line " + Line + " in file '" + FileName + "'";
            }
        }
    }
}
