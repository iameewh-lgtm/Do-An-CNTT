using System.Threading;
using System.Threading.Tasks;

namespace ĐồÁnCơSở.Services
{
    public interface IAIChatService
    {
        Task<string> GenerateCustomerReplyAsync(string userMessage, CancellationToken cancellationToken = default);
        Task<string> GenerateProductDescriptionAsync(string productName, string? categoryName, double? price, CancellationToken cancellationToken = default);
    }
}
