using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ĐồÁnCơSở.Models;
using ĐồÁnCơSở.Utility;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;

namespace ĐồÁnCơSở.Areas.Buyer.Controllers
{
    [Area("Buyer")]
    [Authorize]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;

        public CartController(ApplicationDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ShoppingCartVM cartVM = new()
            {
                ListCart = _db.ShoppingCarts.Include(u => u.Product).Where(u => u.ApplicationUserId == userId).ToList(),
                OrderTotal = 0
            };
            foreach (var cart in cartVM.ListCart)
            {
                // FIX 1: Ép kiểu double của Product.Price sang decimal để cộng dồn
                cartVM.OrderTotal += (decimal)(cart.Product.Price * cart.Count);
            }
            return View(cartVM);
        }

        public IActionResult Summary()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = _db.ApplicationUsers.Find(userId);
            ShoppingCartVM cartVM = new()
            {
                ListCart = _db.ShoppingCarts.Include(u => u.Product).Where(u => u.ApplicationUserId == userId).ToList(),
                OrderTotal = 0,
                CustomerName = user?.Name,
                CustomerPhoneNumber = user?.PhoneNumber,
                StreetAddress = user?.StreetAddress,
                Ward = user?.Ward,
                District = user?.District,
                City = user?.City
            };
            foreach (var cart in cartVM.ListCart)
            {
                // FIX 2: Ép kiểu double của Product.Price sang decimal
                cartVM.OrderTotal += (decimal)(cart.Product.Price * cart.Count);
            }
            return View(cartVM);
        }

        [HttpPost]
        [ActionName("Summary")]
        public IActionResult SummaryPOST(ShoppingCartVM cartVM, string PaymentMethod)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            OrderHeader orderHeader = new()
            {
                ApplicationUserId = userId,
                OrderDate = DateTime.Now,
                OrderStatus = "Chờ xử lý",
                Name = cartVM.CustomerName,
                PhoneNumber = cartVM.CustomerPhoneNumber,
                StreetAddress = cartVM.StreetAddress,
                Ward = cartVM.Ward,
                District = cartVM.District,
                City = cartVM.City
            };
            _db.OrderHeaders.Add(orderHeader);
            _db.SaveChanges();

            var cartList = _db.ShoppingCarts.Include(u => u.Product).Where(u => u.ApplicationUserId == userId).ToList();
            foreach (var item in cartList)
            {
                _db.OrderDetails.Add(new OrderDetail
                {
                    OrderHeaderId = orderHeader.Id,
                    ProductId = item.ProductId,
                    Count = item.Count,
                    // FIX 3: Ép kiểu Giá lưu vào chi tiết đơn hàng
                    Price = (decimal)item.Product.Price
                });

                // FIX 4: Ép kiểu lúc cộng Tổng tiền đơn hàng
                orderHeader.OrderTotal += (decimal)(item.Product.Price * item.Count);
            }
            _db.OrderHeaders.Update(orderHeader);
            _db.SaveChanges();

            if (PaymentMethod == "VNPAY")
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                if (string.IsNullOrEmpty(ipAddress) || ipAddress == "::1")
                {
                    ipAddress = "127.0.0.1";
                }

                string vnp_TmnCode = _config["VnPay:TmnCode"];
                string vnp_HashSecret = _config["VnPay:HashSecret"];
                string vnp_Url = _config["VnPay:BaseUrl"];
                string vnp_ReturnUrl = _config["VnPay:ReturnUrl"];

                VnPayLibrary vnpay = new VnPayLibrary();
                vnpay.AddRequestData("vnp_Version", "2.1.0");
                vnpay.AddRequestData("vnp_Command", "pay");
                vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);

                long amount = (long)(orderHeader.OrderTotal * 100);
                vnpay.AddRequestData("vnp_Amount", amount.ToString());
                vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
                vnpay.AddRequestData("vnp_CurrCode", "VND");
                vnpay.AddRequestData("vnp_IpAddr", ipAddress);
                vnpay.AddRequestData("vnp_Locale", "vn");
                vnpay.AddRequestData("vnp_OrderInfo", "Thanh_toan_don_hang_" + orderHeader.Id);
                vnpay.AddRequestData("vnp_OrderType", "other");
                vnpay.AddRequestData("vnp_ReturnUrl", vnp_ReturnUrl);
                vnpay.AddRequestData("vnp_TxnRef", orderHeader.Id.ToString());

                string paymentUrl = vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);
                return Redirect(paymentUrl);
            }
            else
            {
                orderHeader.OrderStatus = "Đã đặt hàng (" + PaymentMethod + ")";

                foreach (var item in cartList)
                {
                    var p = _db.Products.Find(item.ProductId);
                    if (p != null)
                    {
                        p.Stock -= item.Count;
                        p.SoldQuantity += item.Count;
                    }
                }
                _db.SaveChanges();

                _db.ShoppingCarts.RemoveRange(cartList);
                _db.SaveChanges();

                TempData["success"] = "Đặt hàng thành công!";
                return View("OrderSuccess", orderHeader.Id);
            }
        }

        public IActionResult VnPayReturn()
        {
            if (Request.Query.Count > 0)
            {
                string vnp_ResponseCode = Request.Query["vnp_ResponseCode"];
                string vnp_TxnRef = Request.Query["vnp_TxnRef"];

                if (vnp_ResponseCode == "00")
                {
                    var order = _db.OrderHeaders.Include(u => u.ApplicationUser).FirstOrDefault(u => u.Id == Convert.ToInt32(vnp_TxnRef));
                    if (order != null)
                    {
                        order.OrderStatus = "Đã thanh toán";
                        var details = _db.OrderDetails.Where(u => u.OrderHeaderId == order.Id).ToList();

                        foreach (var item in details)
                        {
                            var p = _db.Products.Find(item.ProductId);
                            if (p != null)
                            {
                                p.Stock -= item.Count;
                                p.SoldQuantity += item.Count;
                            }
                        }
                        _db.SaveChanges();

                        var cartList = _db.ShoppingCarts.Where(u => u.ApplicationUserId == order.ApplicationUserId).ToList();
                        _db.ShoppingCarts.RemoveRange(cartList);
                        _db.SaveChanges();

                        TempData["success"] = "Thanh toán thành công!";
                        return View("OrderSuccess", order.Id);
                    }
                }
            }
            return RedirectToAction("Index", "Home");
        }

        public IActionResult Plus(int cartId)
        {
            var c = _db.ShoppingCarts.Include(u => u.Product).First(u => u.Id == cartId);
            if (c.Count < c.Product.Stock)
            {
                c.Count++;
            }
            _db.SaveChanges();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Minus(int cartId)
        {
            var c = _db.ShoppingCarts.Find(cartId);
            if (c.Count <= 1)
            {
                _db.ShoppingCarts.Remove(c);
            }
            else
            {
                c.Count--;
            }
            _db.SaveChanges();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Remove(int cartId)
        {
            _db.ShoppingCarts.Remove(_db.ShoppingCarts.Find(cartId));
            _db.SaveChanges();
            TempData["success"] = "Đã xóa khỏi giỏ hàng";
            return RedirectToAction(nameof(Index));
        }
    }
}