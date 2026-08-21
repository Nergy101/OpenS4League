using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenS4L;
using OpenS4L.Common;
using OpenS4L.Common.Configuration;
using OpenS4L.Common.Plugins;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.GameRules;
using ProudNet.Hosting.Services;

namespace OpenS4L.Plugins.ExamplePlugin
{
    public class ExamplePlugin : IPlugin
    {
        public void OnInitialize(IConfiguration appConfiguration)
        {
        }

        public void OnConfigure(IServiceCollection services)
        {
            services
                .AddTransient<ExamplePluginGameRule>()
                .AddHostedServiceEx<ExamplePluginService>();
        }

        public void OnShutdown()
        {
        }
    }

    public class ExamplePluginService : IHostedService
    {
        private readonly GameRuleResolver _gameRuleResolver;

        public ExamplePluginService(GameRuleResolver gameRuleResolver)
        {
            _gameRuleResolver = gameRuleResolver;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            RoomManager.RoomCreateHook += OnRoomCreateHook;
            // Channel.JoinHook += OnChannelJoinHook;   // disabled: the channel-4-only rule was an
            //                                          // annoyance for load-testing (bots couldn't
            //                                          // join any channel but id 4). Left as a
            //                                          // commented-out example of the hook API.
            GameRuleBase.CanStartGameHook += OnCanStartGameHook;
            GameRuleBase.HasEnoughPlayersHook += OnHasEnoughPlayersHook;
            _gameRuleResolver.Register(GameRule.Touchdown, x => typeof(ExamplePluginGameRule), 11);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private bool OnRoomCreateHook(RoomCreateHookEventArgs e)
        {
            return true;
        }

        // Example hook: restrict which channels players may join. Deliberately commented out so
        // it never runs in any build; see StartAsync above.
        /*
        private bool OnChannelJoinHook(ChannelJoinHookEventArgs e)
        {
            if (e.Channel.Id == 4)
                return true;

            e.Error = ChannelJoinError.AlreadyInChannel;
            return false;
        }
        */

        private bool OnCanStartGameHook(CanStartGameHookEventArgs e)
        {
            if (e.GameRule.GameRule == GameRule.Deathmatch)
                e.Result = true;

            return true;
        }

        private bool OnHasEnoughPlayersHook(HasEnoughPlayersHookEventArgs e)
        {
            if (e.GameRule.GameRule == GameRule.Deathmatch)
                e.Result = true;

            return true;
        }
    }

    public class ExamplePluginGameRule : Touchdown
    {
        public ExamplePluginGameRule(GameRuleStateMachine stateMachine, IOptions<GameOptions> gameOptions,
            IOptions<TouchdownOptions> options, ISchedulerService schedulerService)
            : base(stateMachine, gameOptions, options, schedulerService)
        {
            GameRuleStateMachine.ScheduleTriggerHook += ScheduleTriggerHook;
        }

        public override void Cleanup()
        {
            base.Cleanup();
            GameRuleStateMachine.ScheduleTriggerHook -= ScheduleTriggerHook;
        }

        protected override bool CanStartGame()
        {
            return true;
        }

        protected override bool HasEnoughPlayers()
        {
            return true;
        }

        private bool ScheduleTriggerHook(ScheduleTriggerHookEventArgs e)
        {
            if (e.StateMachine == StateMachine)
                e.Cancel = true;

            return true;
        }
    }
}
