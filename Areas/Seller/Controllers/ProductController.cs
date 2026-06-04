using ĐồÁnCơSở.Models;
using ĐồÁnCơSở.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ĐồÁnCơSở.Areas.Seller.Controllers
{
    [Area("Seller")]
    [Authorize(Roles = "Admin,Seller")]
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IAIChatService _aiChatService;

        public ProductController(ApplicationDbContext db, IWebHostEnvironment env, IAIChatService aiChatService)
        {
            _db = db;
            _env = env;
            _aiChatService = aiChatService;
        }

        public IActionResult Index()
        {
            try
            {
                var claimsIdentity = (ClaimsIdentity)User.Identity;
                var userId = claimsIdentity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account", new { area = "Buyer" });

                var products = _db.Products.Include(u => u.Category).AsNoTracking();
                return View(User.IsInRole("Admin") ? products.ToList() : products.Where(u => u.SellerId == userId).ToList());
            }
            catch (Exception)
            {
                TempData["error"] = "Lỗi tải danh sách!";
                return View(new List<Product>());
            }
        }

        private void PopulateCategoryList()
        {
            var categories = _db.Categories.ToList();
            if (categories == null || !categories.Any())
            {
                ViewBag.CategoryList = new List<SelectListItem> { new SelectListItem { Text = "Chưa có danh mục", Value = "" } };
            }
            else
            {
                ViewBag.CategoryList = categories.Select(i => new SelectListItem
                {
                    Text = i.Name,
                    Value = i.Id.ToString()
                }).ToList();
            }
        }

        public IActionResult Upsert(int? id)
        {
            PopulateCategoryList();

            if (id == null || id == 0) return View(new Product());

            var product = _db.Products.AsNoTracking().FirstOrDefault(u => u.Id == id);
            return product == null ? NotFound() : View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert(Product obj, IFormFile? file)
        {
            try
            {
                var claimsIdentity = (ClaimsIdentity)User.Identity;
                var userId = claimsIdentity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                obj.SellerId = userId;

                if (file != null)
                {
                    string wwwRootPath = _env.WebRootPath;
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                    string productPath = Path.Combine(wwwRootPath, "images", "products");
                    if (!Directory.Exists(productPath)) Directory.CreateDirectory(productPath);

                    if (!string.IsNullOrEmpty(obj.ImageUrl))
                    {
                        var oldImagePath = Path.Combine(wwwRootPath, obj.ImageUrl.TrimStart('/', '\\'));
                        if (System.IO.File.Exists(oldImagePath)) System.IO.File.Delete(oldImagePath);
                    }

                    using (var fs = new FileStream(Path.Combine(productPath, fileName), FileMode.Create))
                    {
                        await file.CopyToAsync(fs);
                    }
                    obj.ImageUrl = "/images/products/" + fileName;
                }

                ModelState.Remove("Category");
                ModelState.Remove("Seller");
                ModelState.Remove("SellerId");

                if (obj.CategoryId == 0)
                {
                    ModelState.AddModelError("CategoryId", "Vui lòng chọn danh mục.");
                }

                if (ModelState.IsValid)
                {
                    if (obj.Id == 0) _db.Products.Add(obj);
                    else _db.Products.Update(obj);

                    await _db.SaveChangesAsync();
                    TempData["success"] = "Đã lưu sản phẩm thành công!";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["error"] = "Lỗi hệ thống: " + ex.Message;
            }

            PopulateCategoryList();
            return View(obj);
        }


        [HttpPost]
        public async Task<IActionResult> GenerateDescription([FromBody] ProductDescriptionRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ProductName))
            {
                return Json(new { success = false, message = "Vui lòng nhập tên sản phẩm trước." });
            }

            var description = await _aiChatService.GenerateProductDescriptionAsync(
                request.ProductName,
                request.CategoryName,
                request.Price,
                HttpContext.RequestAborted);

            return Json(new { success = true, description });
        }

        public IActionResult Delete(int? id)
        {
            if (id == null || id == 0) return NotFound();
            var product = _db.Products.Include(u => u.Category).FirstOrDefault(u => u.Id == id);
            return product == null ? NotFound() : View(product);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePOST(int? id)
        {
            var obj = await _db.Products.FindAsync(id);
            if (obj == null) return NotFound();

            if (!string.IsNullOrEmpty(obj.ImageUrl))
            {
                var oldPath = Path.Combine(_env.WebRootPath, obj.ImageUrl.TrimStart('/', '\\'));
                if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
            }

            _db.Products.Remove(obj);
            await _db.SaveChangesAsync();
            TempData["success"] = "Đã xóa sản phẩm!";
            return RedirectToAction("Index");
        }
    }
}