using Should;
using Xunit;

namespace Cassette.Stylesheets
{
    public class LessJsFileSearchModifier_Tests
    {
        [Fact]
        public void ModifyAddsLessPattern()
        {
            var modifier = new LessJsFileSearchModifier();
            var fileSearch = new FileSearch();
            modifier.Modify(fileSearch);
            fileSearch.Pattern.ShouldContain("*.less");
        }
    }
}