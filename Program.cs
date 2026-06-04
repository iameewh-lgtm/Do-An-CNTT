using ĐồÁnCơSở.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ĐồÁnCơSở.Hubs;
using ĐồÁnCơSở.Utility;
using ĐồÁnCơSở.Services;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// 1. SERVICES
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IAIChatService, AIChatService>();
builder.Services.AddSignalR();

// 2. DATABASE
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 3. IDENTITY
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<CustomClaimsPrincipalFactory>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Buyer/Account/Login";
    options.LogoutPath = "/Buyer/Account/Logout";
    options.AccessDeniedPath = "/Buyer/Account/AccessDenied";
});

// 3.1. GOOGLE LOGIN
// Chỉ bật Google Login khi đã cấu hình đủ ClientId và ClientSecret trong User Secrets.
// Nếu chưa có, web vẫn chạy bình thường và nút Google sẽ được ẩn ở Login/Register.
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
        });
}

// 4. SESSION
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(100);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// FIX UPLOAD (100MB)
builder.Services.Configure<IISServerOptions>(options => { options.MaxRequestBodySize = 104857600; });
builder.WebHost.ConfigureKestrel(options => { options.Limits.MaxRequestBodySize = 104857600; });
builder.Services.Configure<FormOptions>(options => { options.MultipartBodyLengthLimit = 104857600; });

var app = builder.Build();

// 5. MIDDLEWARE
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Buyer/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

// ✅ 6. SEED DỮ LIỆU (Bản Update chống văng web)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userMgr = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleMgr = services.GetRequiredService<RoleManager<IdentityRole>>();

        // Sử dụng Task.Run để không block luồng chính khởi động web
        Task.Run(async () => {
            await DbInitializer.Initialize(context, userMgr, roleMgr);
        }).Wait();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Lỗi khi Seed dữ liệu!");
    }
}

// 7. ROUTING
app.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(name: "default", pattern: "{area=Buyer}/{controller=Home}/{action=Index}/{id?}");
app.MapHub<ChatHub>("/chatHub");

app.Run();