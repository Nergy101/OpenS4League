using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OpenS4L.Common.Plugins
{
    public interface IPlugin
    {
        void OnInitialize(IConfiguration appConfiguration);

        void OnConfigure(IServiceCollection services);

        void OnShutdown();
    }
}
