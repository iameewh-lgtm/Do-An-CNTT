using Microsoft.AspNetCore.Identity.UI.Services;
using System.Threading.Tasks;

namespace ĐồÁnCơSở.Utility
{
    public class EmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Tạm thời để trống để không bị lỗi build bạn ơi
            // Khi nào bạn muốn gửi mail thiệt thì mình mới cài MailKit sau
            return Task.CompletedTask;
        }
    }
}