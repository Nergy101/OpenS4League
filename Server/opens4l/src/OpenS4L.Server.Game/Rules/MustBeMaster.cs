using System.Threading.Tasks;
using ProudNet;

namespace OpenS4L.Server.Game.Rules
{
    public class MustBeMaster : IFirewallRule
    {
        public Task<bool> IsMessageAllowed(MessageContext context, object message)
        {
            var session = (Session)context.Session;
            var plr = session.Player;
            var room = plr.Room;

            return Task.FromResult(room?.Master == plr);
        }
    }
}
