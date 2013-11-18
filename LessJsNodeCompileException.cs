using System;

namespace Cassette.Stylesheets
{
    public class LessJsNodeCompileException : Exception
    {
        public LessJsNodeCompileException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public LessJsNodeCompileException(string message)
            : base(message)
        {
        }
    }
}

