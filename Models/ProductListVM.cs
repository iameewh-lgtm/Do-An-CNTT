using System.Collections.Generic;

namespace ĐồÁnCơSở.Models
{
    public class ProductListVM
    {
        public IEnumerable<Product> Products { get; set; } = new List<Product>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string? SearchString { get; set; }
        public int TotalItems { get; set; }
        public int? CategoryId { get; set; }
    }
}