using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ĐồÁnCơSở.Models
{
    public class District
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        public string ProvinceId { get; set; } = string.Empty;

        [ForeignKey("ProvinceId")]
        public virtual Province? Province { get; set; }

        public virtual ICollection<Ward>? Wards { get; set; }
    }
}