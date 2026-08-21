using System.Threading.Tasks;

namespace OpenS4L.Database.Helpers
{
    public interface ISaveable
    {
        Task Save(GameContext db);
    }
}
