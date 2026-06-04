using ĐồÁnCơSở.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ĐồÁnCơSở.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Seller")]
    public class SliderController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public SliderController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public IActionResult Index() => View(_db.Sliders.ToList());

        public IActionResult Upsert(int? id)
        {
            if (id == null || id == 0) return View(new Slider());
            var obj = _db.Sliders.Find(id);
            return obj == null ? NotFound() : View(obj);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert(int Id, string Title, string? ImageUrl, IFormFile? file)
        {
            Slider obj = new Slider { Id = Id, Title = Title, ImageUrl = ImageUrl, Status = "Pending" };

            if (file != null)
            {
                string wwwRootPath = _env.WebRootPath;
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                string path = Path.Combine(wwwRootPath, "images", "sliders");

                if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                if (!string.IsNullOrEmpty(obj.ImageUrl))
                {
                    var oldPath = Path.Combine(wwwRootPath, obj.ImageUrl.TrimStart('/').TrimStart('\\'));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                using (var fs = new FileStream(Path.Combine(path, fileName), FileMode.Create))
                {
                    await file.CopyToAsync(fs);
                }
                obj.ImageUrl = "/images/sliders/" + fileName;
            }

            if (obj.Id == 0) _db.Sliders.Add(obj);
            else _db.Sliders.Update(obj);

            await _db.SaveChangesAsync();
            TempData["success"] = "Lưu thành công!";
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Approve(int id)
        {
            var obj = _db.Sliders.Find(id);
            if (obj != null)
            {
                obj.Status = "Approved";
                _db.SaveChanges();
                TempData["success"] = "Đã duyệt Banner!";
            }
            return RedirectToAction("Index");
        }

        public IActionResult Delete(int id)
        {
            var obj = _db.Sliders.Find(id);
            if (obj != null)
            {
                if (!string.IsNullOrEmpty(obj.ImageUrl))
                {
                    var path = Path.Combine(_env.WebRootPath, obj.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                }
                _db.Sliders.Remove(obj);
                _db.SaveChanges();
            }
            return RedirectToAction("Index");
        }
    }
}