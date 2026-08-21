using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace OpenS4L.Common.Plugins
{
    /// <summary>
    /// Simple DI-scan replacement for <see cref="MefPluginHost"/>.
    ///
    /// Behavior-identical to the MEF host: recursively scans a folder for assemblies, finds every
    /// concrete public type implementing <see cref="IPlugin"/>, instantiates one shared instance of
    /// each, and drives the <see cref="IPlugin"/> lifecycle. No System.Composition / MEF dependency.
    ///
    /// Swap-in is a one-liner in each server's Program.cs:
    ///   IPluginHost pluginHost = new ScanPluginHost();   // was: new MefPluginHost();
    /// </summary>
    public class ScanPluginHost : IPluginHost
    {
        private readonly List<IPlugin> _plugins = new List<IPlugin>();

        public void Initialize(IConfiguration configuration, string directory)
        {
            var logger = Log.ForContext<ScanPluginHost>();
            logger.Information("Loading plugins...");

            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories))
                {
                    var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(file);

                    foreach (var type in GetPluginTypes(assembly))
                    {
                        var plugin = (IPlugin)Activator.CreateInstance(type);
                        _plugins.Add(plugin);
                    }
                }
            }

            foreach (var plugin in _plugins)
                plugin.OnInitialize(configuration);

            logger.Information("Loaded {Count} plugins", _plugins.Count);
        }

        public void OnConfigure(IServiceCollection services)
        {
            foreach (var plugin in _plugins)
                plugin.OnConfigure(services);
        }

        public void Dispose()
        {
            foreach (var plugin in _plugins)
                plugin.OnShutdown();
        }

        private static IEnumerable<Type> GetPluginTypes(Assembly assembly)
        {
            return SafeGetTypes(assembly)
                .Where(t => !t.IsAbstract && !t.IsInterface && t.IsPublic
                            && typeof(IPlugin).IsAssignableFrom(t)
                            && t.GetConstructor(Type.EmptyTypes)?.IsPublic == true);
        }

        /// <summary>
        /// An assembly's types can throw <see cref="ReflectionTypeLoadException"/> when one of the
        /// plugin's dependencies can't be resolved. Skip the broken types instead of crashing the
        /// whole server, so a single stale plugin doesn't take everything down.
        /// </summary>
        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                Log.ForContext<ScanPluginHost>()
                    .Warning(ex, "Some types in assembly {Assembly} could not be loaded; skipping them",
                        assembly.FullName);
                return ex.Types.Where(t => t != null);
            }
        }
    }
}
