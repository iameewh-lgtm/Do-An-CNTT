using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ĐồÁnCơSở.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace ĐồÁnCơSở.Areas.Buyer.Controllers
{
    [Area("Buyer")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly int _pageSize = 16;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public HomeController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _db = db;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public IActionResult Index(string searchString, int? categoryId, int productPage = 1)
        {
            ViewBag.CategoryList = _db.Categories.ToList();
            ViewBag.SliderList = _db.Sliders.ToList();
            // Lấy Reels kèm Product để hiện ở trang chủ
            ViewBag.ReelsList = _db.Reels.Include(r => r.Product).OrderByDescending(r => r.Id).Take(10).ToList();

            var products = _db.Products.Include(u => u.Category).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
                products = products.Where(p => p.Name.Contains(searchString));

            if (categoryId != null && categoryId > 0)
                products = products.Where(p => p.CategoryId == categoryId);

            int totalItems = products.Count();

            ProductListVM productListVM = new()
            {
                Products = products.OrderBy(p => p.Id)
                                   .Skip((productPage - 1) * _pageSize)
                                   .Take(_pageSize).ToList(),
                CurrentPage = productPage,
                TotalPages = (int)Math.Ceiling((decimal)totalItems / _pageSize),
                SearchString = searchString,
                CategoryId = categoryId
            };

            return View(productListVM);
        }

        // ==========================================
        // ĐÂY LÀ HÀM MỚI ĐƯỢC THÊM VÀO ĐỂ XỬ LÝ LỖI 404
        // ==========================================
        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Details(int productId)
        {
            var product = _db.Products.Include(u => u.Category).Include(u => u.Seller).FirstOrDefault(u => u.Id == productId);
            if (product == null) return NotFound();

            ShoppingCart cartObj = new() { Count = 1, ProductId = productId, Product = product };

            ViewBag.ReviewList = _db.Reviews.Where(r => r.ProductId == productId)
                .Include(r => r.ApplicationUser).OrderByDescending(r => r.CreatedDate).ToList();

            return View(cartObj);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult Details(ShoppingCart shoppingCart)
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
            shoppingCart.ApplicationUserId = userId;

            var cartFromDb = _db.ShoppingCarts.FirstOrDefault(u => u.ApplicationUserId == userId && u.ProductId == shoppingCart.ProductId);

            if (cartFromDb != null)
            {
                cartFromDb.Count += shoppingCart.Count;
                _db.ShoppingCarts.Update(cartFromDb);
            }
            else
            {
                _db.ShoppingCarts.Add(shoppingCart);
            }

            _db.SaveChanges();
            TempData["success"] = "Đã thêm vào giỏ hàng!";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Reels()
        {
            var reelsList = _db.Reels.Include(r => r.Product).OrderByDescending(r => r.Id).ToList();
            return View(reelsList);
        }

        public async Task<IActionResult> CreateAdmin()
        {
            string[] roleNames = { "Admin", "Seller", "Buyer" };
            foreach (var roleName in roleNames)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                    await _roleManager.CreateAsync(new IdentityRole(roleName));
            }

            var user = await _userManager.FindByEmailAsync("ĐồÁnCơSở@kimi.wasd");
            if (user == null)
            {
                var adminUser = new ApplicationUser { UserName = "ĐồÁnCơSở@kimi.wasd", Email = "ĐồÁnCơSở@kimi.wasd", Name = "Admin", EmailConfirmed = true };
                await _userManager.CreateAsync(adminUser, "Huyhuy13.");
                await _userManager.AddToRoleAsync(adminUser, "Admin");
            }
            return RedirectToAction("Index");
        }
    }
}