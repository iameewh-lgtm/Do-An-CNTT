using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ĐồÁnCơSở.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        public string? StreetAddress { get; set; }
        public string? Ward { get; set; }
        public string? District { get; set; }
        public string? City { get; set; }
    }
}