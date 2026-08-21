using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenS4L.Database.Game
{
    [Table("level_rewards")]
    public class LevelRewardEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Level { get; set; }

        [Column]
        public byte MoneyType { get; set; }

        [Column]
        public int Money { get; set; }
    }
}
