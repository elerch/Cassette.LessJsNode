
namespace Cassette.Stylesheets
{
    public class LessJsNodeFileSearchModifier : IFileSearchModifier<StylesheetBundle>
    {
        public void Modify(FileSearch fileSearch)
        {
            fileSearch.Pattern += ";*.less";
        }
    }
}