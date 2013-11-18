using Should;
using Xunit;

namespace Cassette.Stylesheets
{
    public class LessJsNodeFileSearchModifier_Tests
    {
        [Fact]
        public void ModifyAddsLessPattern()
        {
            var modifier = new LessJsNodeFileSearchModifier();
            var fileSearch = new FileSearch();
            modifier.Modify(fileSearch);
            fileSearch.Pattern.ShouldContain("*.less");
        }
    }
}