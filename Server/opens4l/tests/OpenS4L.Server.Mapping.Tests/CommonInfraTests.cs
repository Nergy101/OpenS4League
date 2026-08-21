using System;
using System.Threading;
using System.Threading.Tasks;
using Foundatio.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OpenS4L;
using OpenS4L.Common;
using OpenS4L.Common.Configuration;
using OpenS4L.Common.Messaging;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Tests for OpenS4L.Common DI helpers, config option POCOs, IdGeneratorService, and the
    /// message-bus request/response helpers.
    /// </summary>
    public class CommonInfraTests
    {
        // ---- IdGeneratorService ----

        [Fact]
        public void IdGeneratorService_generatesIncreasingIds()
        {
            var svc = new IdGeneratorService(
                Microsoft.Extensions.Options.Options.Create(new IdGeneratorOptions { Id = 1 }));
            var first = svc.GetNextId(IdKind.Item);
            var second = svc.GetNextId(IdKind.Item);
            Assert.True(second > first);
        }

        [Fact]
        public void IdGeneratorService_rejectsInvalidServiceId()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new IdGeneratorService(Microsoft.Extensions.Options.Options.Create(new IdGeneratorOptions { Id = 32 })));
        }

        // ---- Config option POCOs (bound from hjson; just verify instantiable with props) ----

        [Fact]
        public void ConfigOptions_areInstantiable()
        {
            var network = new NetworkOptions { Listener = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 22000) };
            Assert.Equal(22000, network.Listener.Port);

            var db = new DatabaseOptions { ConnectionStrings = new ConnectionStrings { Auth = "a", Game = "g", Redis = "r" } };
            Assert.Equal("a", db.ConnectionStrings.Auth);

            var serverList = new ServerListOptions { Id = 1, Name = "test", Address = "127.0.0.1" };
            Assert.Equal("test", serverList.Name);

            var logger = new LoggerOptions { Level = "Information", Directory = "logs", Name = "server" };
            Assert.Equal("Information", logger.Level);

            var clan = new ClanOptions();
            Assert.NotNull(clan);

            var idgen = new IdGeneratorOptions { Id = 3 };
            Assert.Equal(3, idgen.Id);
        }

        // ---- ServiceCollectionExtensions ----

        private interface IFakeService : IService { }
        private class FakeService : IFakeService { }

        [Fact]
        public void AddService_registersSingleton()
        {
            var services = new ServiceCollection();
            services.AddService<IFakeService, FakeService>();
            var provider = services.BuildServiceProvider();
            Assert.NotNull(provider.GetService<IFakeService>());
            Assert.NotNull(provider.GetService<IService>());
        }

        [Fact]
        public void AddHostedServiceEx_registersHostedService()
        {
            var services = new ServiceCollection();
            services.AddHostedServiceEx<FakeHostedService>();
            var provider = services.BuildServiceProvider();
            Assert.NotNull(provider.GetService<FakeHostedService>());
        }

        private class FakeHostedService : Microsoft.Extensions.Hosting.IHostedService
        {
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
        }

        // ---- MessageBusExtensions (foundatio in-memory bus) ----

        private sealed class Req : MessageWithGuid { public int Value; }
        private sealed class Resp : MessageWithGuid { public int Doubled; }

        [Fact]
        public async Task PublishRequest_roundtripsResponse()
        {
            var bus = new InMemoryMessageBus();
            await bus.SubscribeToRequestAsync<Req, Resp>(async req => new Resp { Doubled = req.Value * 2 }, CancellationToken.None);

            var response = await bus.PublishRequestAsync<Req, Resp>(new Req { Value = 21 });
            Assert.Equal(42, response.Doubled);
        }
    }
}
