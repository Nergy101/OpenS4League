using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenS4L.Database.Game
{
    [Table("shop_version")]
    public class ShopVersionEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column]
        [Required]
        [MaxLength(40)]
        public string Version { get; set; }
    }
}
