using ĐồÁnCơSở.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace ĐồÁnCơSở.Utility
{
    public class CustomClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
    {
        public CustomClaimsPrincipalFactory(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> optionsAccessor)
            : base(userManager, roleManager, optionsAccessor)
        {
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            // Lấy danh sách Claims cơ bản (ID, UserName, Roles...)
            var identity = await base.GenerateClaimsAsync(user);

            // 1. ÉP KIỂU TÊN: Đảm bảo không bao giờ NULL để tránh sập View
            string fullName = "Khách hàng";
            if (!string.IsNullOrEmpty(user.Name))
            {
                fullName = user.Name;
            }
            else if (!string.IsNullOrEmpty(user.UserName))
            {
                fullName = user.UserName;
            }

            // Xóa Claim cũ nếu lỡ có trùng để tránh lỗi "dư thừa"
            var existingClaim = identity.FindFirst("FullName");
            if (existingClaim != null) identity.RemoveClaim(existingClaim);

            identity.AddClaim(new Claim("FullName", fullName));

            // 2. THÊM EMAIL (Để hiện ở góc màn hình cho đẹp)
            if (!string.IsNullOrEmpty(user.Email))
            {
                identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
            }

            return identity;
        }
    }
}