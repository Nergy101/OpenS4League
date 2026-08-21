using System.Threading.Tasks;
using ProudNet;

namespace OpenS4L.Server.Game.Rules
{
    public class MustBeInRoom : IFirewallRule
    {
        public Task<bool> IsMessageAllowed(MessageContext context, object message)
        {
            var session = (Session)context.Session;
            return Task.FromResult(session.Player.Room != null);
        }
    }
}
