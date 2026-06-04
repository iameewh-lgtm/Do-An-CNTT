using ĐồÁnCơSở.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ĐồÁnCơSở.Areas.Buyer.Controllers
{
    [Area("Buyer")]
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext db,
            IConfiguration configuration)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated) return RedirectToAction("Index", "Home");
            SetAuthViewBag();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVM model)
        {
            if (ModelState.IsValid)
            {
                var loginValue = model.Email?.Trim() ?? string.Empty;

                var user = await _userManager.FindByEmailAsync(loginValue);
                user ??= await _userManager.FindByNameAsync(loginValue);

                if (user != null)
                {
                    var result = await _signInManager.PasswordSignInAsync(
                        user,
                        model.Password,
                        model.RememberMe,
                        lockoutOnFailure: false
                    );

                    if (result.Succeeded)
                        return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu sai.");
            }
            SetAuthViewBag();
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            if (!IsGoogleLoginConfigured())
            {
                TempData["error"] = "Google Login chưa được cấu hình ClientId/ClientSecret.";
                return RedirectToAction(nameof(Login));
            }

            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { area = "Buyer", returnUrl }, protocol: Request.Scheme);
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            returnUrl ??= Url.Action("Index", "Home", new { area = "Buyer" });
            if (!Url.IsLocalUrl(returnUrl))
            {
                returnUrl = Url.Action("Index", "Home", new { area = "Buyer" });
            }

            if (!string.IsNullOrEmpty(remoteError))
            {
                ModelState.AddModelError(string.Empty, "Đăng nhập Google thất bại: " + remoteError);
                SetAuthViewBag();
                return View("Login", new LoginVM());
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ModelState.AddModelError(string.Empty, "Không lấy được thông tin đăng nhập Google.");
                SetAuthViewBag();
                return View("Login", new LoginVM());
            }

            var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
            if (signInResult.Succeeded)
            {
                return LocalRedirect(returnUrl!);
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var name = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email ?? "Người dùng Google";

            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(string.Empty, "Tài khoản Google không cung cấp email.");
                SetAuthViewBag();
                return View("Login", new LoginVM());
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    Name = name,
                    EmailConfirmed = true
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    foreach (var error in createResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    SetAuthViewBag();
                    return View("Login", new LoginVM());
                }

                if (await _roleManager.RoleExistsAsync("Buyer"))
                {
                    await _userManager.AddToRoleAsync(user, "Buyer");
                }
            }

            var logins = await _userManager.GetLoginsAsync(user);
            if (!logins.Any(l => l.LoginProvider == info.LoginProvider && l.ProviderKey == info.ProviderKey))
            {
                await _userManager.AddLoginAsync(user, info);
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect(returnUrl!);
        }

        [HttpGet]
        public IActionResult Register()
        {
            ViewBag.RoleList = GetRoleSelectList();
            SetAuthViewBag();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterVM model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email, Name = model.Name };
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    string selectedRole = model.Role;
                    if (selectedRole == "Admin")
                    {
                        selectedRole = "Buyer"; 
                    }

                    if (await _roleManager.RoleExistsAsync(selectedRole))
                    {
                        await _userManager.AddToRoleAsync(user, selectedRole);
                    }
                    else
                    {
                        await _userManager.AddToRoleAsync(user, "Buyer");
                    }

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Index", "Home");
                }
                foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            }
            ViewBag.RoleList = GetRoleSelectList();
            SetAuthViewBag();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UserList()
        {
            var users = await _db.ApplicationUsers.ToListAsync();
            var userVMList = new List<UserVM>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userVMList.Add(new UserVM
                {
                    Id = user.Id,
                    Name = user.Name ?? "Chưa đặt tên",
                    Email = user.Email,
                    Role = roles.FirstOrDefault() ?? "Buyer"
                });
            }
            return View(userVMList);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignRole(string userId)
        {
            var user = await _db.ApplicationUsers.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound();

            var roles = await _roleManager.Roles.ToListAsync();
            var userRoles = await _userManager.GetRolesAsync(user);

            ViewBag.UserName = user.Name;
            ViewBag.UserId = userId;
            ViewBag.CurrentRole = userRoles.FirstOrDefault();

            return View(roles);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRole(string userId, string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, roleName);

            TempData["success"] = "Đã đổi quyền thành công";
            return RedirectToAction(nameof(UserList));
        }

        private List<SelectListItem> GetRoleSelectList()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Text = "Người mua (Buyer)", Value = "Buyer" },
                new SelectListItem { Text = "Người bán (Seller)", Value = "Seller" }
            };
        }

        private bool IsGoogleLoginConfigured()
        {
            return !string.IsNullOrWhiteSpace(_configuration["Authentication:Google:ClientId"])
                && !string.IsNullOrWhiteSpace(_configuration["Authentication:Google:ClientSecret"]);
        }

        private void SetAuthViewBag()
        {
            ViewBag.GoogleLoginEnabled = IsGoogleLoginConfigured();
        }

        public IActionResult AccessDenied() => View();
    }
}