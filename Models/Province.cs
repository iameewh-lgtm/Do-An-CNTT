using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ĐồÁnCơSở.Models
{
    public class Province
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        public virtual ICollection<District>? Districts { get; set; }
    }
}