using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ĐồÁnCơSở.Models;
using Microsoft.AspNetCore.Authorization;

namespace ĐồÁnCơSở.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")] // Chỉ ông trùm mới được vô đây dòm tiền
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AdminController(ApplicationDbContext db)
        {
            _db = db;
        }

        public IActionResult Dashboard()
        {
            // 1. Tổng doanh thu
            ViewBag.TotalRevenue = _db.OrderHeaders.Sum(u => u.OrderTotal);

            // 2. Tổng số đơn hàng
            ViewBag.TotalOrders = _db.OrderHeaders.Count();

            // 3. Tổng số khách hàng (Buyer)
            ViewBag.TotalUsers = _db.ApplicationUsers.Count();

            // 4. Top 5 sản phẩm bán chạy nhứt
            var topProducts = _db.OrderDetails
                .Include(u => u.Product)
                .GroupBy(u => u.ProductId)
                .Select(g => new
                {
                    ProductName = g.First().Product.Name,
                    TotalQty = g.Sum(x => x.Count)
                })
                .OrderByDescending(x => x.TotalQty)
                .Take(5).ToList();

            return View(topProducts);
        }
    }
}