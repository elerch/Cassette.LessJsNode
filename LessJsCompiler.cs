using Cassette.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Storage = System.IO.IsolatedStorage;

namespace Cassette.Stylesheets
{
    public class LessJsCompiler : ILessJsCompiler
    {
        private CompileResult compileResult;
        private string createdDirectory;
        public CompileResult Compile(string source, CompileContext context)
        {
            var sourceFile = context.RootDirectory.GetFile(context.SourceFilePath);
            sourceFile = EnsureExists(source, sourceFile, context.RootDirectory);
            NormalizeSourceFile(sourceFile, source);
            CompileResult result;
            try {
                result = Compile(sourceFile);
            }
            catch (Exception ex) {
                throw new LessJsCompileException(
                    string.Format("Error compiling {0}{1}{2}", context.SourceFilePath, Environment.NewLine, ex.Message),
                    ex
                );
            }
            return result;
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
            if (!sourceFile.Exists){
                using (var reader = sourceFile.OpenRead()) {
                    if (reader == null) {
                        Copy(sourceFile, destinationDirectoryInfo.Item1, () => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(source)));
                        excludeFile = sourceFile;
                    }
                }
            }
            // Marshal to something on the filesystem - lessc.wsf depends on it!
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
            while (Directory.Exists(folder) || File.Exists(folder)){
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
            ProcessStartInfo start = new ProcessStartInfo(@"cscript");
            start.WindowStyle = ProcessWindowStyle.Hidden;
            start.CreateNoWindow = true;
            start.Arguments = "//nologo \"" + GetExecutablePath() + "\" \"" + absolutePath + "\" \"" + output + "\"" + " -filenames";
            start.EnvironmentVariables["output"] = output;
            start.EnvironmentVariables["fileName"] = source.FullPath;
            start.UseShellExecute = false;
            start.RedirectStandardError = true;
            start.RedirectStandardOutput = true;

            Process p = new Process();
            p.StartInfo = start;
            p.EnableRaisingEvents = true;
            p.Exited += ProcessExited;
            p.Start();

            const int SLEEP_AMOUNT = 20;
            var elapsedTime = new TimeSpan(0);
            while (compileResult == null && (elapsedTime = elapsedTime.Add(new TimeSpan(0,0,0,0,SLEEP_AMOUNT))).TotalSeconds <= 30)
                System.Threading.Thread.Sleep(SLEEP_AMOUNT);           
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
                    throw new LessJsCompileException("Less compilation failure", ex);
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
                        paths = reader.ReadToEnd().Split('\n').Where(s => !string.IsNullOrWhiteSpace(s));
                    }
                } else {
                    using (StreamReader reader = process.StandardError) {
                        throw new LessJsCompileException(ParseError(reader.ReadToEnd()).ToString());
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
            string wscript = tempPath + "lessc.wsf";
            string es5shim = tempPath + "es5-shim.min.js";
            string less = tempPath + "less-1.4.2.min.js";

            CreateIfNotExists(new Dictionary<string, string>{
                {wscript, "lessc"},
                {es5shim, "es5"}, 
                {less, "less"}
            });

            return wscript;
        }

        private static void CreateIfNotExists(IEnumerable<KeyValuePair<string, string>> files)
        {
            foreach (var kvp in files)
                CreateIfNotExists(kvp.Key, kvp.Value);
        }

        private static void CreateIfNotExists(string path, string resourceKey)
        {
            if (File.Exists(path)) return;
            string data = Resources.ResourceManager.GetString(resourceKey);
            File.WriteAllText(path, data);
        }

        private class CompilerError
        {
            public int Line { get; set; }
            public int Column { get; set; }
            public string FileName { get; set; }
            public string Message { get; set; }
        }
    }
}
