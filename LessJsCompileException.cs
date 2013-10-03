using System;

namespace Cassette.Stylesheets
{
    public class LessJsCompileException : Exception
    {
        public LessJsCompileException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public LessJsCompileException(string message)
            : base(message)
        {
        }
    }
}

