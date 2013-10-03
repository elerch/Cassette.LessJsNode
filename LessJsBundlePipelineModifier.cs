using Cassette.BundleProcessing;

namespace Cassette.Stylesheets
{
    public class LessJsBundlePipelineModifier : IBundlePipelineModifier<StylesheetBundle>
    {
        public IBundlePipeline<StylesheetBundle> Modify(IBundlePipeline<StylesheetBundle> pipeline)
        {
            var index = pipeline.IndexOf<ParseCssReferences>();
            pipeline.Insert<ParseJsLessReferences>(index + 1);
            pipeline.Insert<CompileLessWithJs>(index+2);

            return pipeline;
        }
    }
}