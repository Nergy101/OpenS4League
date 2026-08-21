using System.Threading.Tasks;
using OpenS4L.Common.Cryptography;
using OpenS4L.Database;
using OpenS4L.Database.Auth;
using OpenS4L.Server.Game.Services;

namespace OpenS4L.Server.Game.Commands
{
    internal class AccountCommands : ICommandHandler
    {
        private readonly DatabaseService _databaseService;

        public AccountCommands(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        [Command(
            CommandUsage.Player | CommandUsage.Console,
            SecurityLevel.Administrator,
            "Usage: createaccount <username> <password>"
        )]
        public async Task<bool> CreateAccount(Player plr, string[] args)
        {
            if (args.Length != 2)
                return false;

            var username = args[0];
            var password = args[1];
            var (hash, salt) = PasswordHasher.Hash(password);

            using (var db = _databaseService.Open<AuthContext>())
            {
                var accountEntity = new AccountEntity
                {
                    Username = username,
                    Password = hash,
                    Salt = salt,
                    SecurityLevel = (byte)SecurityLevel.User
                };
                db.Accounts.Add(accountEntity);
                await db.SaveChangesAsync();
                this.Reply(plr, $"Created account with username={username} id={accountEntity.Id}");
            }

            return true;
        }
    }
}
