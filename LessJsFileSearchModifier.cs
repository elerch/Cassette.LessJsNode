
namespace Cassette.Stylesheets
{
    public class LessJsFileSearchModifier : IFileSearchModifier<StylesheetBundle>
    {
        public void Modify(FileSearch fileSearch)
        {
            fileSearch.Pattern += ";*.less";
        }
    }
}