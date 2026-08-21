using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using OpenS4L.Common;
using OpenS4L.Common.Configuration.Hjson;
using OpenS4L.Common.Plugins;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Tests for OpenS4L.Common configuration loading (Hjson provider), Startup bootstrap, and
    /// the ScanPluginHost plugin loader. These cover the bootstrapping that every server runs.
    /// </summary>
    public class CommonBootstrapTests : IDisposable
    {
        private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "opens4l-common-tests-" + Guid.NewGuid());

        public CommonBootstrapTests()
        {
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        private string WriteConfig(string content, string name = "config.hjson")
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public void AddHjsonFile_loadsConfiguration()
        {
            var path = WriteConfig("{\n  Network: {\n    Listener: 0.0.0.0:22000\n  },\n  MaxLevel: 99\n}");
            var builder = new ConfigurationBuilder();
            builder.AddHjsonFile(path, optional: false);
            var config = builder.Build();
            Assert.Equal("99", config["MaxLevel"]);
            Assert.Equal("0.0.0.0:22000", config["Network:Listener"]);
        }

        [Fact]
        public void AddHjsonFile_isOptional_whenMissing()
        {
            var builder = new ConfigurationBuilder();
            builder.AddHjsonFile(Path.Combine(_tempDir, "missing.hjson"), optional: true);
            var config = builder.Build();
            Assert.Null(config["Anything"]);
        }

        [Fact]
        public void AddHjsonFile_validatesArguments()
        {
            var builder = new ConfigurationBuilder();
            // null path throws ArgumentException (path is validated before the null-builder check)
            Assert.Throws<ArgumentException>(() => builder.AddHjsonFile((IFileProvider)null, null, false, false));
            Assert.Throws<ArgumentException>(() => builder.AddHjsonFile("", false));
        }

        [Fact]
        public void Startup_Initialize_loadsConfigAndReturns()
        {
            // Startup.Initialize sets global JsonConvert settings + type converters + serilog,
            // then loads the hjson config. We just assert it returns a config with our value.
            var path = WriteConfig("{\n  MaxLevel: 77\n}");
            var config = Startup.Initialize(_tempDir, Path.GetFileName(path),
                c => new OpenS4L.Common.Configuration.LoggerOptions { Level = "Warning", Directory = "logs", Name = "test" });
            Assert.Equal("77", config["MaxLevel"]);
        }

        [Fact]
        public void ScanPluginHost_emptyDirectory_doesNothing()
        {
            var host = new ScanPluginHost();
            var emptyDir = Path.Combine(_tempDir, "empty-plugins");
            Directory.CreateDirectory(emptyDir);
            host.Initialize(new ConfigurationBuilder().Build(), emptyDir);
            host.OnConfigure(new Microsoft.Extensions.DependencyInjection.ServiceCollection());
            host.Dispose(); // no plugins, so no-op
        }

        [Fact]
        public void ScanPluginHost_nonexistentDirectory_doesNothing()
        {
            var host = new ScanPluginHost();
            host.Initialize(new ConfigurationBuilder().Build(), Path.Combine(_tempDir, "does-not-exist"));
            host.Dispose();
        }
    }
}
