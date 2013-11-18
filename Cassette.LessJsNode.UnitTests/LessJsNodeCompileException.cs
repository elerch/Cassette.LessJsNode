using Should;
using Xunit;

namespace Cassette.Stylesheets
{
    public class LessJsNodeCompileException_Tests
    {
        [Fact]
        public void LessCompileExceptionConstructorAcceptsMessage()
        {
            new LessJsNodeCompileException("test").Message.ShouldEqual("test");
        }
    }
}

