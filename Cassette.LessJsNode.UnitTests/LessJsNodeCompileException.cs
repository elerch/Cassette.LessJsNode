using Should;
using Xunit;

namespace Cassette.Stylesheets
{
    public class LessJsCompileException_Tests
    {
        [Fact]
        public void LessCompileExceptionConstructorAcceptsMessage()
        {
            new LessJsNodeCompileException("test").Message.ShouldEqual("test");
        }
    }
}

