using System.Collections.Generic;
using OpenS4L;

namespace OpenS4L.Plugins.WebApi.Models
{
    public class MapDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public IList<GameRule> GameRules { get; set; }
    }
}
