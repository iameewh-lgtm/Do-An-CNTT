using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ĐồÁnCơSở.Models;
using ĐồÁnCơSở.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ĐồÁnCơSở.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _db;
        private readonly IAIChatService _aiChatService;

        public ChatHub(ApplicationDbContext db, IAIChatService aiChatService)
        {
            _db = db;
            _aiChatService = aiChatService;
        }

        public async Task SendMessage(string senderId, string receiverId, string message)
        {
            var currentUserId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(currentUserId))
            {
                // Không tin hoàn toàn senderId từ JavaScript để tránh giả mạo người gửi.
                senderId = currentUserId;
            }

            if (string.IsNullOrWhiteSpace(senderId) || string.IsNullOrWhiteSpace(receiverId) || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            message = message.Trim();
            if (message.Length > 2000)
            {
                message = message.Substring(0, 2000);
            }

            // 1. Lưu tin nhắn vào Database để có lịch sử
            var msg = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = message,
                Timestamp = DateTime.Now,
                IsAIResponse = false
            };
            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();

            // 2. Bắn tin nhắn qua cho người nhận nếu họ đang mở web
            await Clients.User(receiverId).SendAsync("ReceiveMessage", senderId, message);

            // 3. Bắn ngược lại cho người gửi để hiện lên màn hình của họ
            await Clients.Caller.SendAsync("ReceiveMessage", senderId, message);

            // 4. Nếu người nhận là AI_BOT thì trả lời tự động.
            // Ưu tiên đọc Product/Category từ database trước, sau đó mới gọi GPT/Gemini cho câu hỏi hỗ trợ chung.
            if (receiverId == "AI_BOT")
            {
                await Clients.Caller.SendAsync("BotTyping");

                string? databaseReply = await TryGenerateDatabaseProductReplyAsync(message);
                string aiReply = databaseReply ?? await _aiChatService.GenerateCustomerReplyAsync(message, Context.ConnectionAborted);

                var aiMsg = new Message
                {
                    SenderId = "AI_BOT",
                    ReceiverId = senderId,
                    Content = aiReply,
                    Timestamp = DateTime.Now,
                    IsAIResponse = true
                };
                _db.Messages.Add(aiMsg);
                await _db.SaveChangesAsync();

                await Task.Delay(700);
                await Clients.Caller.SendAsync("ReceiveMessage", "AI_BOT", aiReply);
            }
        }

        private async Task<string?> TryGenerateDatabaseProductReplyAsync(string message)
        {
            var normalizedMessage = NormalizeText(message);
            var keywords = ExtractKeywords(normalizedMessage);

            bool isProductQuestion = IsProductQuestion(normalizedMessage) || keywords.Count == 1;
            if (!isProductQuestion && keywords.Count == 0)
            {
                return null;
            }

            var products = await _db.Products
                .Include(p => p.Category)
                .Include(p => p.Seller)
                .AsNoTracking()
                .OrderByDescending(p => p.SoldQuantity)
                .ThenByDescending(p => p.Id)
                .Take(300)
                .ToListAsync(Context.ConnectionAborted);

            if (products.Count == 0)
            {
                return isProductQuestion
                    ? "🤖 Trợ lý KIMI: Hiện database chưa có sản phẩm nào để mình tra cứu. Bạn hãy liên hệ Admin hoặc quay lại sau nhé."
                    : null;
            }

            var matchedProducts = products
                .Select(p => new
                {
                    Product = p,
                    Score = CalculateMatchScore(p, keywords, normalizedMessage)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Product.SoldQuantity)
                .Take(5)
                .Select(x => x.Product)
                .ToList();

            if (matchedProducts.Any())
            {
                return BuildProductReply(matchedProducts, message);
            }

            if (IsGeneralProductListQuestion(normalizedMessage))
            {
                var topProducts = products.Take(5).ToList();
                return BuildProductReply(topProducts, message, "Mình chưa thấy bạn nhập tên sản phẩm cụ thể, dưới đây là một vài sản phẩm đang có trong database:");
            }

            if (isProductQuestion)
            {
                return "🤖 Trợ lý KIMI: Mình đã kiểm tra database nhưng chưa tìm thấy sản phẩm khớp với câu hỏi của bạn. Bạn thử nhập lại tên sản phẩm ngắn hơn, ví dụ: \"Mambo\", hoặc dùng ô tìm kiếm trên trang chủ nhé.";
            }

            return null;
        }

        private static bool IsProductQuestion(string normalizedMessage)
        {
            string[] signals =
            {
                "san pham", "tim", "mua", "gia", "bao nhieu", "con hang", "het hang",
                "danh muc", "shop", "nguoi ban", "chi tiet", "mambo", "ao", "quan", "giay", "tui"
            };
            return signals.Any(normalizedMessage.Contains);
        }

        private static bool IsGeneralProductListQuestion(string normalizedMessage)
        {
            string[] signals =
            {
                "co san pham gi", "san pham nao", "goi y san pham", "ban gi", "co gi ban", "xem san pham"
            };
            return signals.Any(normalizedMessage.Contains);
        }

        private static List<string> ExtractKeywords(string normalizedMessage)
        {
            var stopWords = new HashSet<string>
            {
                "toi", "minh", "muon", "can", "tim", "kiem", "mua", "xem", "co", "khong",
                "san", "pham", "hang", "hoa", "cai", "nay", "kia", "gi", "la", "bao", "nhieu",
                "cho", "hoi", "ve", "cua", "ban", "shop", "admin", "lien", "he", "voi", "mot",
                "nhung", "cac", "nhe", "a", "da", "dang", "duoc", "trong", "database"
            };

            return Regex.Matches(normalizedMessage, "[a-z0-9]+")
                .Select(m => m.Value)
                .Where(w => w.Length >= 2 && !stopWords.Contains(w))
                .Distinct()
                .Take(8)
                .ToList();
        }

        private static int CalculateMatchScore(Product product, List<string> keywords, string normalizedMessage)
        {
            if (keywords.Count == 0) return 0;

            var name = NormalizeText(product.Name ?? string.Empty);
            var description = NormalizeText(product.Description ?? string.Empty);
            var category = NormalizeText(product.Category?.Name ?? string.Empty);
            var seller = NormalizeText(product.Seller?.Name ?? string.Empty);

            int score = 0;
            foreach (var keyword in keywords)
            {
                if (name == keyword) score += 100;
                if (name.Contains(keyword)) score += 60;
                if (category.Contains(keyword)) score += 30;
                if (seller.Contains(keyword)) score += 20;
                if (description.Contains(keyword)) score += 10;
            }

            // Nếu người dùng nhập nguyên cụm gần giống tên sản phẩm thì cộng điểm cao hơn.
            if (!string.IsNullOrWhiteSpace(name) && normalizedMessage.Contains(name)) score += 120;

            return score;
        }

        private static string BuildProductReply(List<Product> products, string originalMessage, string? intro = null)
        {
            var vi = CultureInfo.GetCultureInfo("vi-VN");
            var sb = new StringBuilder();
            sb.Append("🤖 Trợ lý KIMI: ");
            sb.Append(intro ?? $"Mình đã đọc database và tìm thấy {products.Count} sản phẩm phù hợp với câu hỏi của bạn:");
            sb.AppendLine();

            foreach (var p in products)
            {
                var price = string.Format(vi, "{0:N0} VNĐ", p.Price);
                var category = string.IsNullOrWhiteSpace(p.Category?.Name) ? "Chưa có danh mục" : p.Category.Name;
                var seller = string.IsNullOrWhiteSpace(p.Seller?.Name) ? "Chưa rõ người bán" : p.Seller.Name;
                var stockText = p.Stock > 0 ? $"Còn {p.Stock} sản phẩm" : "Có thể đang hết hàng";
                var shortDescription = Shorten(p.Description, 120);

                sb.AppendLine($"- {p.Name} | Giá: {price} | Danh mục: {category} | {stockText} | Người bán: {seller}");
                if (!string.IsNullOrWhiteSpace(shortDescription))
                {
                    sb.AppendLine($"  Mô tả: {shortDescription}");
                }
                sb.AppendLine($"  Xem chi tiết: /Buyer/Home/Details?productId={p.Id}");
            }

            sb.Append("Bạn có thể bấm vào link chi tiết hoặc nhập tên sản phẩm vào ô tìm kiếm trên trang chủ để xem nhanh hơn nhé.");
            return sb.ToString();
        }

        private static string Shorten(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            value = Regex.Replace(value.Trim(), "\\s+", " ");
            return value.Length <= maxLength ? value : value.Substring(0, maxLength).Trim() + "...";
        }

        private static string NormalizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            value = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var chars = value.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray();
            value = new string(chars).Normalize(NormalizationForm.FormC);
            value = value.Replace('đ', 'd').Replace('Đ', 'd');
            value = Regex.Replace(value, "[^a-z0-9\\s]", " ");
            value = Regex.Replace(value, "\\s+", " ").Trim();
            return value;
        }
    }
}
