using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;
using RayFluxMarket.Models.Entities;

namespace RayFluxMarket.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlMessage);                                                                             
                                                                                                                                                              
        Task SendOrderConfirmationAsync(Order order, User user);                                                                                              
        Task SendPaymentReceiptAsync(Order order, User user);                                                                                                 
        Task SendPasswordResetAsync(User user, string resetLink); 
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        
        public Task SendOrderConfirmationAsync(Order order, User user)                                                                                        
        {                                                                                                                                                     
            var subject = $"Заказ #{order.Id} оформлен";                                                                                                      
            var html = $@"                                                                                                                                    
                <h2>Спасибо за заказ, {user.FullName ?? user.Email}!</h2>                                                                                     
                <p>Ваш заказ <b>#{order.Id}</b> на сумму <b>{order.TotalAmount:C}</b> принят.</p>                                                             
                <p>Мы свяжемся с вами для уточнения деталей доставки.</p>";                                                                                   
            return SendEmailAsync(user.Email, subject, html);                                                                                                 
        }                                                                                                                                                     
                                                                                                                                                              
        public Task SendPaymentReceiptAsync(Order order, User user)                                                                                           
        {                                                                                                                                                     
            var subject = $"Чек по заказу #{order.Id}";                                                                                                       
            var html = $@"                                                                                                                                    
                <h2>Оплата получена</h2>                                                                                                                      
                <p>Заказ <b>#{order.Id}</b> успешно оплачен на сумму <b>{order.TotalAmount:C}</b>.</p>                                                        
                <p>Спасибо, что выбрали нас!</p>";                                                                                                            
            return SendEmailAsync(user.Email, subject, html);                                                                                                 
        }                                                                                                                                                     
                                                                                                                                                              
        public Task SendPasswordResetAsync(User user, string resetLink)                                                                                       
        {                                                                                                                                                     
            var subject = "Сброс пароля";                                                                                                                     
            var html = $@"                                                                                                                                    
                <h2>Сброс пароля</h2>                                                                                                                         
                <p>Для сброса пароля перейдите по ссылке:</p>                                                                                                 
                <p><a href=""{resetLink}"">Сбросить пароль</a></p>                                                                                            
                <p>Если вы не запрашивали сброс — просто проигнорируйте это письмо.</p>";                                                                     
            return SendEmailAsync(user.Email, subject, html);                                                                                                 
        }    

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(
                emailSettings["SenderName"] ?? "RayFluxMarket",
                emailSettings["SenderEmail"]
            ));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = htmlMessage };
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            try
            {
                await smtp.ConnectAsync(
                    emailSettings["SmtpServer"],
                    int.Parse(emailSettings["Port"] ?? "587"),
                    MailKit.Security.SecureSocketOptions.StartTls
                );
                await smtp.AuthenticateAsync(emailSettings["Username"], emailSettings["Password"]);
                await smtp.SendAsync(email);
            }
            finally
            {
                await smtp.DisconnectAsync(true);
            }
        }
    }
}