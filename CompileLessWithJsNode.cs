using System;
using Cassette.BundleProcessing;

namespace Cassette.Stylesheets
{
    public class CompileLessWithJsNode : IBundleProcessor<StylesheetBundle>
    {
        readonly ILessJsNodeCompiler lessCompiler;
        readonly CassetteSettings settings;

        public CompileLessWithJsNode(ILessJsNodeCompiler lessCompiler, CassetteSettings settings)
        {
            this.lessCompiler = lessCompiler;
            this.settings = settings;
        }

        public void Process(StylesheetBundle bundle)
        {
            foreach (var asset in bundle.Assets)
            {
                if (asset.Path.EndsWith(".less", StringComparison.OrdinalIgnoreCase))
                {
                    asset.AddAssetTransformer(new CompileAsset(lessCompiler, settings.SourceDirectory));
                }
            }
        }
    }
}