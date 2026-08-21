using System.Threading.Tasks;
using ProudNet;

namespace OpenS4L.Server.Game.Rules
{
    public class MustBeInClan : IFirewallRule
    {
        public Task<bool> IsMessageAllowed(MessageContext context, object message)
        {
            var session = (Session)context.Session;
            var plr = session.Player;

            return Task.FromResult(plr.Clan != null);
        }
    }
}
