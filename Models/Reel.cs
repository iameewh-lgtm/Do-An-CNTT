using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ĐồÁnCơSở.Models
{
    public class Reel
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề video")]
        [Display(Name = "Tiêu đề video")]
        public string Title { get; set; }

        [Display(Name = "Mô tả ngắn")]
        public string? Description { get; set; }

        // Đường dẫn file video (ví dụ: /videos/reels/video1.mp4)
        [Required]
        [ValidateNever]
        public string VideoUrl { get; set; }

        // Đường dẫn ảnh bìa video (hiển thị khi video chưa load xong)
        [ValidateNever]
        public string? ThumbnailUrl { get; set; }

        // Liên kết với sản phẩm (Quan trọng để bán hàng)
        [Required(ErrorMessage = "Vui lòng chọn sản phẩm gắn kèm")]
        [Display(Name = "Sản phẩm gắn kèm")]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        [ValidateNever]
        public Product Product { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}