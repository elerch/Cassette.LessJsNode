using Should;
using Xunit;

namespace Cassette.Stylesheets
{
    public class LessJsCompileException_Tests
    {
        [Fact]
        public void LessCompileExceptionConstructorAcceptsMessage()
        {
            new LessJsCompileException("test").Message.ShouldEqual("test");
        }
    }
}

