using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OpenS4L.Common.Plugins
{
    public interface IPluginHost : IDisposable
    {
        void Initialize(IConfiguration configuration, string directory);

        void OnConfigure(IServiceCollection services);
    }
}
