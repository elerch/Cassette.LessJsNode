using Cassette.BundleProcessing;

namespace Cassette.Stylesheets
{
    public class LessJsNodeBundlePipelineModifier : IBundlePipelineModifier<StylesheetBundle>
    {
        public IBundlePipeline<StylesheetBundle> Modify(IBundlePipeline<StylesheetBundle> pipeline)
        {
            var index = pipeline.IndexOf<ParseCssReferences>();
            pipeline.Insert<ParseNodeJsLessReferences>(index + 1);
            pipeline.Insert<CompileLessWithJsNode>(index+2);

            return pipeline;
        }
    }
}