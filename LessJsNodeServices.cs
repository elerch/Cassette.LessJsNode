using Cassette.TinyIoC;

namespace Cassette.Stylesheets
{
    [ConfigurationOrder(20)]
    public class LessJsNodeServices : IConfiguration<TinyIoCContainer>
    {
        public void Configure(TinyIoCContainer container)
        {
            container.Register<ILessJsNodeCompiler, LessJsNodeCompiler>().AsMultiInstance();
        }
    }
}