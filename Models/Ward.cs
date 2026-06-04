using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ĐồÁnCơSở.Models
{
    public class Ward
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        public string DistrictId { get; set; } = string.Empty;

        [ForeignKey("DistrictId")]
        public virtual District? District { get; set; }
    }
}