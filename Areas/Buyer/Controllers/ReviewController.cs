using ĐồÁnCơSở.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;

namespace ĐồÁnCơSở.Areas.Buyer.Controllers
{
    [Area("Buyer")]
    [Authorize]
    public class ReviewController : Controller
    {
        private readonly ApplicationDbContext _db;

        public ReviewController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SubmitReview(Review objReview)
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            objReview.ApplicationUserId = claim.Value;

            objReview.CreatedDate = DateTime.Now;

            // Loại bỏ kiểm tra hợp lệ cho các thuộc tính liên kết tự động
            ModelState.Remove("ApplicationUserId"); // Đã thêm dòng này để fix lỗi
            ModelState.Remove("ApplicationUser");
            ModelState.Remove("Product");

            if (ModelState.IsValid)
            {
                _db.Reviews.Add(objReview);
                _db.SaveChanges();
                TempData["success"] = "Cảm ơn bạn đã đánh giá sản phẩm!";
                return RedirectToAction("Details", "Home", new { productId = objReview.ProductId });
            }

            TempData["error"] = "Vui lòng chọn số sao và nhập nội dung đánh giá.";
            return RedirectToAction("Details", "Home", new { productId = objReview.ProductId });
        }
    }
}