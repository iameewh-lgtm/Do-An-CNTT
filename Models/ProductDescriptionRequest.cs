namespace ĐồÁnCơSở.Models
{
    public class ProductDescriptionRequest
    {
        public string ProductName { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public double? Price { get; set; }
    }
}
