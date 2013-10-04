using System;
using System.Collections.Generic;
using System.IO;
using Cassette.IO;
using Cassette.Utilities;
using Trace = Cassette.Diagnostics.Trace;

namespace Cassette.Stylesheets
{
    public class LessJsCompiler : ILessJsCompiler
    {
        HashedSet<string> importedFilePaths;

        public CompileResult Compile(string source, CompileContext context)
        {
            var sourceFile = context.RootDirectory.GetFile(context.SourceFilePath);
            importedFilePaths = new HashedSet<string>();
            //var parser = new Parser
            //{
            //    Importer = new Importer(new CassetteLessFileReader(sourceFile.Directory, importedFilePaths))
            //};
            //var errorLogger = new ErrorLogger();
            //var engine = new LessEngine(parser, errorLogger, false, false);

            //string css;
            try
            {
                //css = engine.TransformToCss(source, sourceFile.FullPath);
            }
            catch (Exception ex)
            {
                throw new LessJsCompileException(
                    string.Format("Error compiling {0}{1}{2}", context.SourceFilePath, Environment.NewLine, ex.Message),
                    ex
                );
            }
            return null;
            //if (errorLogger.HasErrors)
            //{
            //    var exceptionMessage = string.Format(
            //        "Error compiling {0}{1}{2}",
            //        context.SourceFilePath,
            //        Environment.NewLine,
            //        errorLogger.ErrorMessage
            //    );
            //    throw new LessJsCompileException(exceptionMessage);
            //}
            //else
            //{
            //    return new CompileResult(css, importedFilePaths);
            //}
        }
    }
}