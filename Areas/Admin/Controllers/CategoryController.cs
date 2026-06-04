using Microsoft.AspNetCore.Mvc;
using ĐồÁnCơSở.Models;

namespace ĐồÁnCơSở.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class CategoryController : Controller
    {
        private readonly ApplicationDbContext _db;

        // Ép cái Database vô đây để xài
        public CategoryController(ApplicationDbContext db)
        {
            _db = db;
        }

        // Trang hiển thị danh sách
        public IActionResult Index()
        {
            IEnumerable<Category> objCategoryList = _db.Categories.ToList();
            return View(objCategoryList);
        }

        // Trang mở form tạo mới (GET)
        public IActionResult Create()
        {
            return View();
        }

        // Nhấn nút Lưu data xuống DB (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Category obj)
        {
            if (ModelState.IsValid)
            {
                _db.Categories.Add(obj);
                _db.SaveChanges(); // Lệnh này là lưu cái rụp xuống DB nè
                return RedirectToAction("Index");
            }
            return View(obj);
        }
        public IActionResult Edit(int? id)
        {
            if (id == null || id == 0)
            {
                return NotFound();
            }
            var categoryFromDb = _db.Categories.Find(id); // Tìm danh mục theo Id

            if (categoryFromDb == null)
            {
                return NotFound();
            }

            return View(categoryFromDb);
        }

        // Nhấn nút Lưu sau khi sửa (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Category obj)
        {
            if (ModelState.IsValid)
            {
                _db.Categories.Update(obj);
                _db.SaveChanges(); // Cập nhật xuống Database
                return RedirectToAction("Index");
            }
            return View(obj);
        }

        // ================= XÓA (DELETE) =================
        // Mở trang xác nhận Xóa (GET)
        public IActionResult Delete(int? id)
        {
            if (id == null || id == 0)
            {
                return NotFound();
            }
            var categoryFromDb = _db.Categories.Find(id);

            if (categoryFromDb == null)
            {
                return NotFound();
            }

            return View(categoryFromDb);
        }

        // Nhấn nút xác nhận Xóa luôn (POST)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePOST(int? id)
        {
            var obj = _db.Categories.Find(id);
            if (obj == null)
            {
                return NotFound();
            }

            _db.Categories.Remove(obj);
            _db.SaveChanges(); // Xóa rẹt khỏi Database
            return RedirectToAction("Index");
        }
    }
}