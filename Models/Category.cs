using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ĐồÁnCơSở.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Nhập tên danh mục!")]
        [Display(Name = "Tên danh mục")]
        public string Name { get; set; } = string.Empty;

        // THÊM CỘT LƯU ĐƯỜNG DẪN ẢNH (Cho phép null với dấu ?)
        [Display(Name = "Đường dẫn ảnh")]
        public string? ImageUrl { get; set; }

        public virtual ICollection<Product>? Products { get; set; }
    }
}