using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ĐồÁnCơSở.Services
{
    public class AIChatService : IAIChatService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AIChatService> _logger;

        public AIChatService(HttpClient httpClient, IConfiguration configuration, ILogger<AIChatService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> GenerateCustomerReplyAsync(string userMessage, CancellationToken cancellationToken = default)
        {
            var systemPrompt = "Bạn là trợ lý chăm sóc khách hàng của website thương mại điện tử KIMI MARKET. " +
                               "Luôn trả lời bằng tiếng Việt, thân thiện, ngắn gọn, lịch sự. " +
                               "Bạn chỉ hỗ trợ các vấn đề liên quan đến tìm kiếm sản phẩm, thêm vào giỏ hàng, đặt hàng, thanh toán COD/VNPAY, tài khoản, xem đơn hàng, liên hệ Admin hoặc người bán. " +
                               "Nếu khách hỏi thông tin không có trong hệ thống như trạng thái đơn hàng cụ thể, số điện thoại riêng hoặc dữ liệu cá nhân, hãy hướng dẫn khách liên hệ Admin/Shop thay vì tự bịa thông tin.";

            var fallback = GenerateFallbackCustomerResponse(userMessage);
            return await GenerateAsync(systemPrompt, userMessage, fallback, cancellationToken, addBotPrefix: true);
        }

        public async Task<string> GenerateProductDescriptionAsync(string productName, string? categoryName, double? price, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(productName))
            {
                return "Vui lòng nhập tên sản phẩm trước khi tạo mô tả bằng AI.";
            }

            var userPrompt = $"Tên sản phẩm: {productName}\nDanh mục: {categoryName ?? "Chưa chọn"}\nGiá: {(price.HasValue ? price.Value.ToString("N0") + " VNĐ" : "Chưa nhập")}\nHãy viết mô tả sản phẩm ngắn gọn, dễ hiểu, phù hợp website thương mại điện tử.";
            var systemPrompt = "Bạn hỗ trợ người bán trên KIMI MARKET viết mô tả sản phẩm. " +
                               "Trả lời bằng tiếng Việt, khoảng 3 đến 5 câu, không phóng đại quá mức, không cam kết công dụng không có căn cứ, không thêm thông tin kỹ thuật nếu người bán chưa cung cấp.";

            var fallback = GenerateFallbackProductDescription(productName, categoryName, price);
            return await GenerateAsync(systemPrompt, userPrompt, fallback, cancellationToken, addBotPrefix: false);
        }

        private async Task<string> GenerateAsync(string systemPrompt, string userPrompt, string fallback, CancellationToken cancellationToken, bool addBotPrefix)
        {
            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                return fallback;
            }

            var provider = (_configuration["AI:Provider"] ?? string.Empty).Trim().ToLowerInvariant();
            var openAiKey = _configuration["OpenAI:ApiKey"];
            var geminiKey = _configuration["Gemini:ApiKey"];

            if (provider == "openai" || (!string.IsNullOrWhiteSpace(openAiKey) && provider != "gemini"))
            {
                return await GenerateWithOpenAIAsync(systemPrompt, userPrompt, fallback, cancellationToken, addBotPrefix);
            }

            if (provider == "gemini" || !string.IsNullOrWhiteSpace(geminiKey))
            {
                return await GenerateWithGeminiAsync(systemPrompt, userPrompt, fallback, cancellationToken, addBotPrefix);
            }

            return fallback;
        }

        private async Task<string> GenerateWithOpenAIAsync(string systemPrompt, string userPrompt, string fallback, CancellationToken cancellationToken, bool addBotPrefix)
        {
            var apiKey = _configuration["OpenAI:ApiKey"];
            var model = _configuration["OpenAI:Model"];

            if (string.IsNullOrWhiteSpace(apiKey)) return fallback;
            if (string.IsNullOrWhiteSpace(model)) model = "gpt-4o-mini";

            try
            {
                var requestBody = new
                {
                    model,
                    input = new object[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt.Trim() }
                    },
                    temperature = 0.4,
                    max_output_tokens = 500
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = JsonContent.Create(requestBody);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("OpenAI API returned status code {StatusCode}", response.StatusCode);
                    return fallback;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var answer = ExtractOpenAIText(json);
                return string.IsNullOrWhiteSpace(answer) ? fallback : NormalizeAnswer(answer, addBotPrefix);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI API call failed");
                return fallback;
            }
        }

        private async Task<string> GenerateWithGeminiAsync(string systemPrompt, string userPrompt, string fallback, CancellationToken cancellationToken, bool addBotPrefix)
        {
            var apiKey = _configuration["Gemini:ApiKey"];
            var model = _configuration["Gemini:Model"];

            if (string.IsNullOrWhiteSpace(apiKey)) return fallback;
            if (string.IsNullOrWhiteSpace(model)) model = "gemini-1.5-flash";

            try
            {
                var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent";

                var requestBody = new
                {
                    systemInstruction = new
                    {
                        parts = new[] { new { text = systemPrompt } }
                    },
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[] { new { text = userPrompt.Trim() } }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.4,
                        maxOutputTokens = 500
                    }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Add("x-goog-api-key", apiKey);
                request.Content = JsonContent.Create(requestBody);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Gemini API returned status code {StatusCode}", response.StatusCode);
                    return fallback;
                }

                var result = await response.Content.ReadFromJsonAsync<GeminiGenerateContentResponse>(cancellationToken: cancellationToken);
                var answer = result?.Candidates?
                    .FirstOrDefault()?
                    .Content?
                    .Parts?
                    .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Text))?
                    .Text;

                return string.IsNullOrWhiteSpace(answer) ? fallback : NormalizeAnswer(answer, addBotPrefix);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gemini API call failed");
                return fallback;
            }
        }

        private static string ExtractOpenAIText(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
                {
                    return outputText.GetString() ?? string.Empty;
                }

                if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in output.EnumerateArray())
                    {
                        if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) continue;
                        foreach (var contentItem in content.EnumerateArray())
                        {
                            if (contentItem.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                                return text.GetString() ?? string.Empty;
                        }
                    }
                }
            }
            catch
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private static string NormalizeAnswer(string answer, bool addBotPrefix)
        {
            answer = answer.Trim();
            if (!addBotPrefix) return answer;
            return answer.StartsWith("🤖") ? answer : "🤖 Trợ lý KIMI: " + answer;
        }

        private static string GenerateFallbackCustomerResponse(string input)
        {
            input = (input ?? string.Empty).ToLowerInvariant();

            if (input.Contains("chào") || input.Contains("hello") || input.Contains("hi"))
                return "🤖 Trợ lý KIMI: Xin chào! Mình là trợ lý KIMI MARKET. Bạn cần hỗ trợ tìm sản phẩm, đặt hàng hay thanh toán ạ?";

            if (input.Contains("giá") || input.Contains("tiền") || input.Contains("bao nhiêu"))
                return "🤖 Trợ lý KIMI: Giá sản phẩm được hiển thị trực tiếp tại trang chi tiết sản phẩm. Bạn có thể bấm vào sản phẩm để xem giá, mô tả và thông tin người bán nhé.";

            if (input.Contains("mua") || input.Contains("đặt hàng") || input.Contains("giỏ hàng"))
                return "🤖 Trợ lý KIMI: Để đặt hàng, bạn chọn sản phẩm, bấm thêm vào giỏ hàng, kiểm tra số lượng, nhập địa chỉ giao hàng rồi xác nhận đơn hàng.";

            if (input.Contains("thanh toán") || input.Contains("vnpay") || input.Contains("cod"))
                return "🤖 Trợ lý KIMI: KIMI MARKET hỗ trợ COD và VNPAY sandbox. Nếu chọn VNPAY, hệ thống sẽ chuyển bạn sang cổng thanh toán thử nghiệm.";

            if (input.Contains("đơn hàng") || input.Contains("trạng thái") || input.Contains("giao hàng"))
                return "🤖 Trợ lý KIMI: Bạn có thể xem lại đơn hàng trong mục đơn hàng của tài khoản. Nếu cần kiểm tra chi tiết giao hàng, bạn nên nhắn Admin hoặc người bán.";

            if (input.Contains("admin") || input.Contains("hỗ trợ") || input.Contains("liên hệ"))
                return "🤖 Trợ lý KIMI: Bạn có thể chọn mục Admin Sàn trong danh sách tin nhắn để trao đổi trực tiếp với quản trị viên.";

            return "🤖 Trợ lý KIMI: Mình đã ghi nhận câu hỏi của bạn. Bạn có thể nói rõ hơn về sản phẩm, đặt hàng, thanh toán hoặc tài khoản để mình hỗ trợ chính xác hơn nhé.";
        }

        private static string GenerateFallbackProductDescription(string productName, string? categoryName, double? price)
        {
            var category = string.IsNullOrWhiteSpace(categoryName) ? "sản phẩm" : categoryName;
            var priceText = price.HasValue && price.Value > 0 ? $" với mức giá {price.Value:N0} VNĐ" : string.Empty;
            return $"{productName} là {category.ToLower()} phù hợp cho nhu cầu mua sắm hằng ngày{priceText}. Sản phẩm có thiết kế dễ sử dụng, thông tin rõ ràng và phù hợp với nhiều đối tượng khách hàng. Đây là lựa chọn đáng tham khảo trên KIMI MARKET cho người dùng đang tìm kiếm sản phẩm chất lượng và tiện lợi.";
        }

        private class GeminiGenerateContentResponse
        {
            [JsonPropertyName("candidates")]
            public GeminiCandidate[]? Candidates { get; set; }
        }

        private class GeminiCandidate
        {
            [JsonPropertyName("content")]
            public GeminiContent? Content { get; set; }
        }

        private class GeminiContent
        {
            [JsonPropertyName("parts")]
            public GeminiPart[]? Parts { get; set; }
        }

        private class GeminiPart
        {
            [JsonPropertyName("text")]
            public string? Text { get; set; }
        }
    }
}
