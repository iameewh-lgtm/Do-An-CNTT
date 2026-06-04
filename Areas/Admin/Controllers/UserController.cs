using ĐồÁnCơSở.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ĐồÁnCơSở.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public UserController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userList = _db.ApplicationUsers.ToList();
            var userRoles = new Dictionary<string, string>();

            // Quét từng user để lấy chức vụ (Role) của họ ra
            foreach (var user in userList)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userRoles[user.Id] = roles.FirstOrDefault() ?? "Khách hàng";
            }

            ViewBag.UserRoles = userRoles;
            return View(userList);
        }

        [HttpPost]
        public IActionResult LockUnlock([FromBody] string id)
        {
            var objFromDb = _db.ApplicationUsers.FirstOrDefault(u => u.Id == id);
            if (objFromDb == null)
            {
                return Json(new { success = false, message = "Hổng tìm thấy bạn này!" });
            }

            if (objFromDb.LockoutEnd != null && objFromDb.LockoutEnd > DateTime.Now)
            {
                objFromDb.LockoutEnd = DateTime.Now;
            }
            else
            {
                objFromDb.LockoutEnd = DateTime.Now.AddYears(100);
            }

            _db.SaveChanges();
            return Json(new { success = true, message = "Thao tác thành công rầu!" });
        }

        // HÀM MỚI: Xử lý đổi tên User
        [HttpPost]
        public IActionResult UpdateName(string id, string newName)
        {
            var user = _db.ApplicationUsers.FirstOrDefault(u => u.Id == id);
            if (user == null) return Json(new { success = false, message = "Không tìm thấy User!" });

            user.Name = newName;
            _db.SaveChanges();

            return Json(new { success = true, message = "Đổi tên thành công!" });
        }
    }
}