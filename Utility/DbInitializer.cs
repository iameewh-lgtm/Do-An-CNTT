using ĐồÁnCơSở.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ĐồÁnCơSở.Utility
{
    public static class DbInitializer
    {
        public static async Task Initialize(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            try
            {
                // 1. CHỈ MIGRATE KHI CẦN (Đã bọc try-catch để không bao giờ sập)
                try
                {
                    if (db.Database.GetPendingMigrations().Any())
                    {
                        await db.Database.MigrateAsync();
                    }
                }
                catch { /* Bỏ qua nếu có lỗi migration để web vẫn lên được */ }

                // 2. TẠO ROLE (Chủ chốt)
                string[] roleNames = { "Admin", "Buyer", "Seller" };
                foreach (var roleName in roleNames)
                {
                    if (!await roleManager.RoleExistsAsync(roleName))
                    {
                        await roleManager.CreateAsync(new IdentityRole(roleName));
                    }
                }

                // 3. TẠO ADMIN VIP (Dùng tài khoản của bạn)
                var adminEmail = "ĐồÁnCơSở@kimi.wasd";
                var adminUser = await userManager.FindByEmailAsync(adminEmail);

                if (adminUser == null)
                {
                    var user = new ApplicationUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        Name = "Nguyen Nhat Huy", // Đã update theo ý bạn
                        PhoneNumber = "0379941531",
                        EmailConfirmed = true
                    };

                    var result = await userManager.CreateAsync(user, "Huyhuy13.");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, "Admin");
                    }
                }


                // 4. TẠO TÀI KHOẢN ẢO CHO AI_BOT
                // Bảng Messages có khóa ngoại tới AspNetUsers, nên AI_BOT cần có user riêng để lưu lịch sử chat ổn định.
                var botUser = await userManager.FindByIdAsync("AI_BOT");
                if (botUser == null)
                {
                    botUser = new ApplicationUser
                    {
                        Id = "AI_BOT",
                        UserName = "ai_bot@kimi.local",
                        Email = "ai_bot@kimi.local",
                        Name = "Trợ lý AI KIMI",
                        EmailConfirmed = true,
                        PhoneNumber = "0000000000"
                    };

                    await userManager.CreateAsync(botUser, "KimiBot@2026");
                }

                // 5. TẠO CATEGORY MẶC ĐỊNH (Nếu bảng trống)
                if (!db.Categories.Any())
                {
                    db.Categories.AddRange(
                        new Category { Name = "Sách" },
                        new Category { Name = "Thực Phẩm" },
                        new Category { Name = "Điện thoại" },
                        new Category { Name = "Gia dụng" }
                    );
                    await db.SaveChangesAsync();
                }

                // 6. TẠO SLIDER MẪU (Để tránh lỗi null khi mới chạy web)
                if (!db.Sliders.Any())
                {
                    db.Sliders.Add(new Slider
                    {
                        Title = "Chào mừng tới KIMI",
                        Status = "Approved",
                        ImageUrl = "/Images/mascot.png"
                    });
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                // In ra lỗi tại cửa sổ Output của Visual Studio để debug
                System.Diagnostics.Debug.WriteLine("CRITICAL SEED ERROR: " + ex.Message);
            }
        }

        // Tuyệt đối chưa mở cái này ra khi chưa xử lý xong file JSON nhé!
        public static Task SeedLocations(ApplicationDbContext db)
        {
            return Task.CompletedTask;
        }
    }
}