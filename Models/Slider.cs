using System.ComponentModel.DataAnnotations;

namespace ĐồÁnCơSở.Models
{
    public class Slider
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề banner")]
        public string Title { get; set; }

        public string Status { get; set; } = "Pending";

        public string? ImageUrl { get; set; }
    }
}