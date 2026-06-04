using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ĐồÁnCơSở.Models;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Security.Claims;
using System.Collections.Generic;

namespace ĐồÁnCơSở.Areas.Buyer.Controllers
{
    [Area("Buyer")]
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _db;

        public OrderController(ApplicationDbContext db)
        {
            _db = db;
        }

        public IActionResult Index()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            IEnumerable<OrderHeader> orderHeaders;
            double totalMoney = 0;

            if (User.IsInRole("Admin"))
            {
                // ADMIN: Thấy mọi đơn, Doanh thu là 5% PHÍ SÀN từ các đơn "Đã hoàn thành"
                orderHeaders = _db.OrderHeaders.Include(u => u.ApplicationUser).OrderByDescending(u => u.Id).ToList();
                double totalPlatformSales = (double)orderHeaders.Where(o => o.OrderStatus == "Đã hoàn thành").Sum(o => o.OrderTotal);
                totalMoney = totalPlatformSales * 0.05; // Ăn 5% phí sàn
                ViewBag.RoleContext = "Admin";
            }
            else if (User.IsInRole("Seller"))
            {
                // SELLER: Thấy đơn có hàng của mình, Doanh thu là 95% từ hàng "Đã hoàn thành"
                var sellerOrderDetails = _db.OrderDetails
                    .Include(o => o.OrderHeader)
                    .Include(o => o.Product)
                    .Where(o => o.Product.SellerId == userId).ToList();

                var orderHeaderIds = sellerOrderDetails.Select(o => o.OrderHeaderId).Distinct();

                orderHeaders = _db.OrderHeaders.Include(u => u.ApplicationUser)
                                               .Where(o => orderHeaderIds.Contains(o.Id)).OrderByDescending(u => u.Id).ToList();

                double myTotalSales = (double)sellerOrderDetails
                    .Where(o => o.OrderHeader.OrderStatus == "Đã hoàn thành")
                    .Sum(o => o.Count * o.Price);
                totalMoney = myTotalSales * 0.95; // Seller nhận 95%
                ViewBag.RoleContext = "Seller";
            }
            else
            {
                // BUYER (KHÁCH): Chỉ thấy đơn mình đặt, Thống kê là Tổng tiền đã tiêu
                orderHeaders = _db.OrderHeaders.Include(u => u.ApplicationUser)
                                               .Where(u => u.ApplicationUserId == userId).OrderByDescending(u => u.Id).ToList();
                totalMoney = (double)orderHeaders.Where(o => o.OrderStatus != "Đã hủy").Sum(o => o.OrderTotal);
                ViewBag.RoleContext = "Buyer";
            }

            ViewBag.TotalMoney = totalMoney;
            return View(orderHeaders);
        }

        public IActionResult Details(int orderId)
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            OrderVM orderVM = new OrderVM()
            {
                OrderHeader = _db.OrderHeaders.Include(u => u.ApplicationUser).FirstOrDefault(u => u.Id == orderId),
                OrderDetails = _db.OrderDetails.Include(u => u.Product).ThenInclude(p => p.Seller).Where(u => u.OrderHeaderId == orderId).ToList()
            };

            if (orderVM.OrderHeader == null) return NotFound();

            if (!User.IsInRole("Admin") && !User.IsInRole("Seller") && orderVM.OrderHeader.ApplicationUserId != userId)
            {
                return RedirectToAction("Index");
            }

            if (User.IsInRole("Seller") && !User.IsInRole("Admin"))
            {
                orderVM.OrderDetails = orderVM.OrderDetails.Where(u => u.Product.SellerId == userId).ToList();
            }

            return View(orderVM);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult UpdateStatus(int orderId, string status)
        {
            var orderHeader = _db.OrderHeaders.Find(orderId);
            if (orderHeader != null)
            {
                orderHeader.OrderStatus = status;
                _db.SaveChanges();
            }
            return RedirectToAction(nameof(Details), new { orderId });
        }

        // HÀM XÁC NHẬN NHẬN HÀNG CHO BUYER
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmReceipt(int orderId)
        {
            var orderHeader = _db.OrderHeaders.Find(orderId);
            if (orderHeader != null && orderHeader.OrderStatus != "Đã hoàn thành" && orderHeader.OrderStatus != "Đã hủy")
            {
                orderHeader.OrderStatus = "Đã hoàn thành"; // Chốt đơn
                _db.SaveChanges();
                TempData["success"] = "Đã nhận hàng thành công! Hệ thống đã chia tiền cho Shop.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}