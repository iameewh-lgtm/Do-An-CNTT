using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ĐồÁnCơSở.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        public string Description { get; set; }
        [Required]
        public double Price { get; set; }
        public int Stock { get; set; }
        public int SoldQuantity { get; set; }
        public string ImageUrl { get; set; }

        [Required]
        public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        [ValidateNever]
        public Category Category { get; set; }

        // MẤU CHỐT LÀ ĐÂY: Khai báo chủ sở hữu để chia doanh thu
        public string? SellerId { get; set; }
        [ForeignKey("SellerId")]
        [ValidateNever]
        public ApplicationUser Seller { get; set; }
    }
}