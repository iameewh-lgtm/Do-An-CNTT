using Microsoft.AspNetCore.Mvc;
using ĐồÁnCơSở.Models;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace ĐồÁnCơSở.Areas.Buyer.Controllers
{
    [Area("Buyer")]
    [Authorize] // Bắt buộc đăng nhập mới được vô Chat
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ChatController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string userId)
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var currentUserId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            // Lấy danh sách tất cả User trên sàn (loại trừ chính mình và tài khoản ảo AI_BOT)
            var allUsers = await _db.ApplicationUsers
                .Where(u => u.Id != currentUserId && u.Id != "AI_BOT")
                .ToListAsync();

            // Tìm tài khoản Admin để ghim lên đầu
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            var admin = adminUsers.FirstOrDefault();

            ViewBag.CurrentUserId = currentUserId;
            ViewBag.AdminId = admin?.Id;
            ViewBag.AllUsers = allUsers;

            // Người đang được chọn để chat (Mặc định mở tab lên là gặp Trợ lý AI đầu tiên)
            string activeChatId = userId ?? "AI_BOT";
            ViewBag.ActiveChatId = activeChatId;

            if (activeChatId == "AI_BOT")
            {
                ViewBag.ActiveChatName = "Trợ lý AI KIMI";
            }
            else
            {
                var targetUser = await _db.ApplicationUsers.FindAsync(activeChatId);
                // Dùng biến .Name như đã fix ở bài trước
                ViewBag.ActiveChatName = targetUser?.Name ?? targetUser?.UserName ?? "Người dùng";
            }

            // Kéo lịch sử chat của 2 người
            var messages = _db.Messages
                .Where(m => (m.SenderId == currentUserId && m.ReceiverId == activeChatId) ||
                            (m.SenderId == activeChatId && m.ReceiverId == currentUserId))
                .OrderBy(m => m.Timestamp)
                .ToList();

            return View(messages);
        }
    }
}