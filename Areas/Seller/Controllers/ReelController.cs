using ĐồÁnCơSở.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ĐồÁnCơSở.Areas.Seller.Controllers
{
    [Area("Seller")]
    [Authorize(Roles = "Admin,Seller")]
    public class ReelController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public ReelController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public IActionResult Index() => View(_db.Reels.Include(r => r.Product).ToList());

        public IActionResult Upsert(int? id)
        {
            ViewBag.ProductList = new SelectList(_db.Products, "Id", "Name");
            if (id == null || id == 0) return View(new Reel());

            var reel = _db.Reels.Find(id);
            return reel == null ? NotFound() : View(reel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert(Reel obj, IFormFile? file)
        {
            // Bọc try-catch để nếu lỗi file nặng quá không làm sập web
            try
            {
                if (file != null)
                {
                    string wwwRootPath = _env.WebRootPath;
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                    string path = Path.Combine(wwwRootPath, "videos", "reels");

                    if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                    // Xóa video cũ nếu đang cập nhật
                    if (!string.IsNullOrEmpty(obj.VideoUrl))
                    {
                        var oldPath = Path.Combine(wwwRootPath, obj.VideoUrl.TrimStart('/', '\\'));
                        if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                    }

                    using (var fs = new FileStream(Path.Combine(path, fileName), FileMode.Create))
                    {
                        await file.CopyToAsync(fs);
                    }
                    obj.VideoUrl = "/videos/reels/" + fileName;
                }
                else if (obj.Id == 0)
                {
                    TempData["error"] = "Phải up video lên mới đăng được ní ơi!";
                    ViewBag.ProductList = new SelectList(_db.Products, "Id", "Name");
                    return View(obj);
                }

                // Xóa check validation của Product vì mình chỉ cần ProductId
                ModelState.Remove("Product");

                if (ModelState.IsValid)
                {
                    obj.Status = "Pending"; // Mặc định là chờ duyệt
                    if (obj.Id == 0) _db.Reels.Add(obj);
                    else _db.Reels.Update(obj);

                    await _db.SaveChangesAsync();
                    TempData["success"] = "Đăng Video thành công!";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["error"] = "Lỗi up video: " + ex.Message;
            }

            ViewBag.ProductList = new SelectList(_db.Products, "Id", "Name");
            return View(obj);
        }

        public IActionResult Delete(int id)
        {
            var obj = _db.Reels.Find(id);
            if (obj != null)
            {
                if (!string.IsNullOrEmpty(obj.VideoUrl))
                {
                    var path = Path.Combine(_env.WebRootPath, obj.VideoUrl.TrimStart('/'));
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                }
                _db.Reels.Remove(obj);
                _db.SaveChanges();
                TempData["success"] = "Đã xóa video!";
            }
            return RedirectToAction("Index");
        }
    }
}