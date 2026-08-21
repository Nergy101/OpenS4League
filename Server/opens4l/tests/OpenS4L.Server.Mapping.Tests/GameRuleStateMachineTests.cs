using System;
using System.Reflection;
using Microsoft.Extensions.Options;
using OpenS4L.Common.Configuration;
using OpenS4L.Network.Message.GameRule;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.GameRules;
using ProudNet.Hosting.Services;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Tests for the GameRuleStateMachine's state mapping and transition wiring. We drive the
    /// underlying Stateless state machine directly (via reflection) so we don't need the full
    /// Room / message-bus graph; the transition configuration is the production code under test.
    /// </summary>
    public class GameRuleStateMachineTests
    {
        private static GameRuleStateMachine Create(ISchedulerService sched)
        {
            return new GameRuleStateMachine(sched);
        }

        private static void Initialize(GameRuleStateMachine sm, GameRuleBase gameRule,
            bool canStart, bool hasHalfTime, bool hasTimeLimit)
        {
            var method = typeof(GameRuleStateMachine).GetMethod(
                nameof(GameRuleStateMachine.Initialize),
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            method!.Invoke(sm, new object[] { gameRule, (Func<bool>)(() => canStart), hasHalfTime, hasTimeLimit });
        }

        [Fact]
        public void InitialState_IsWaiting()
        {
            var sm = Create(new NoopSchedulerService());
            Initialize(sm, new FakeGameRule(sm), canStart: true, hasHalfTime: true, hasTimeLimit: true);
            Assert.Equal(GameState.Waiting, sm.GameState);
            Assert.Equal(GameTimeState.FirstHalf, sm.TimeState);
        }

        [Fact]
        public void Uninitialized_GameTime_IsZero()
        {
            var sm = Create(new NoopSchedulerService());
            Initialize(sm, new FakeGameRule(sm), canStart: true, hasHalfTime: true, hasTimeLimit: true);

            // Before the match starts, no round/game start time is set.
            Assert.Equal(TimeSpan.Zero, sm.GameTime);
            Assert.Equal(TimeSpan.Zero, sm.RoundTime);
        }

        // NOTE: transition tests beyond Waiting require a fully-constructed Room (GameRuleResolver,
        // message bus, channel) because GameRuleStateMachine.OnTransition dereferences
        // _gameRule.Room for every non-Waiting state. That's the same heavy graph as the
        // integration fixtures; left out of the unit layer on purpose. Add them alongside a
        // Room fixture if/when one exists.

        private sealed class FakeGameRule : GameRuleBase
        {
            public FakeGameRule(GameRuleStateMachine sm)
                : base(sm, Options.Create(new GameOptions()))
            {
            }

            public override GameRule GameRule => GameRule.Practice;
            public override bool HasHalfTime => true;
            public override bool HasTimeLimit => true;
            protected override bool CanStartGame() => true;
            protected override bool HasEnoughPlayers() => true;
            protected override PlayerScore CreateScore(Player plr) => new PracticePlayerScore();
            protected override BriefingPlayer CreateBriefingPlayer(Player plr) => null;
            protected override (uint, uint) CalculateExperienceGained(Player plr) => (0, 0);
            protected override (uint, uint) CalculatePENGained(Player plr) => (0, 0);
        }
    }
}
