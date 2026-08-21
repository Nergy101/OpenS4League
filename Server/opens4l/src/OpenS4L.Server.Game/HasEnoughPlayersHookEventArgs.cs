using System;

namespace OpenS4L.Server.Game
{
    public class HasEnoughPlayersHookEventArgs : EventArgs
    {
        public GameRuleBase GameRule { get; }
        public bool? Result { get; set; }

        public HasEnoughPlayersHookEventArgs(GameRuleBase gameRule)
        {
            GameRule = gameRule;
        }
    }
}
