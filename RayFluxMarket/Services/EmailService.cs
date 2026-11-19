using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Options;
using RayFluxMarket.Configuration;
using RayFluxMarket.Models.Entities;
using System.Text;
namespace RayFluxMarket.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlMessage);                                                                             
                                                                                                                                                              
        Task SendOrderConfirmationAsync(Order order, User user);                                                                                              
        Task SendPaymentReceiptAsync(Order order, User user);                                                                                                 
        Task SendPasswordResetAsync(User user, string resetCode);
        Task SendWelcomeAsync(User user);
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;

        public EmailService(IOptions<EmailSettings> emailOptions)
        {
            _emailSettings = emailOptions.Value;
        }
        
        public Task SendOrderConfirmationAsync(Order order, User user)
        {
            var itemsHtml = new StringBuilder();
            foreach (var item in order.OrderItems)
            {
                decimal itemTotal = item.Price * item.Quantity;
                string productName = item.Product?.Name ?? $"Товар #{item.ProductId}";
                itemsHtml.Append($@"
                <tr>
                    <td style='padding: 8px; border-bottom: 1px solid #ddd;'>{productName}</td>
                    <td style='padding: 8px; border-bottom: 1px solid #ddd; text-align: center;'>{item.Quantity} шт.</td>
                    <td style='padding: 8px; border-bottom: 1px solid #ddd; text-align: right;'>{itemTotal:N2} ₸</td>
                </tr>");
            }

            var subject = $"Заказ #{order.Id} оформлен — RayFluxMarket";
            var html = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; color: #333; max-width: 600px;'>
                    <h2 style='color: #4f46e5;'>Спасибо за заказ, {user.Email}!</h2>
                    <p>Ваш заказ <b>#{order.Id}</b> на сумму <b>{order.TotalAmount:N2} ₸</b> принят.</p>

                    <h3 style='margin-top: 20px;'>Состав заказа:</h3>
                    <table style='width: 100%; border-collapse: collapse; margin-top: 10px;'>
                        <thead>
                            <tr style='background-color: #f3f4f6;'>
                                <th style='padding: 8px; text-align: left;'>Товар</th>
                                <th style='padding: 8px; text-align: center;'>Кол-во</th>
                                <th style='padding: 8px; text-align: right;'>Сумма</th>
                            </tr>
                        </thead>
                        <tbody>
                            {itemsHtml}
                        </tbody>
                    </table>

                    <p style='margin-top: 15px;'>Мы свяжемся с вами для уточнения деталей доставки.</p>
                    <br>
                    <p>С уважением, <br><b>Команда RayFluxMarket</b></p>
                </div>";
            return SendEmailAsync(user.Email, subject, html);
        }
                                                                                                                                                              
        public Task SendPaymentReceiptAsync(Order order, User user)                                                                                           
        {                                                                                                                                                     
            var itemsHtml = new StringBuilder();                                                                                                              
            decimal totalAmount = 0;                                                                                                                          
                                                                                                                                                              
            foreach (var item in order.OrderItems)                                                                                                            
            {                                                                                                                                                 
                decimal itemTotal = item.Price * item.Quantity;                                                                                               
                totalAmount += itemTotal;                                                                                                                     
                string productName = item.Product?.Name ?? $"Товар #{item.ProductId}";                                                                        
                                                                                                                                                              
                itemsHtml.Append($@"                                                                                                                          
                <tr>                                                                                                                                          
                    <td style='padding: 8px; border-bottom: 1px solid #ddd;'>{productName}</td>                                                               
                    <td style='padding: 8px; border-bottom: 1px solid #ddd; text-align: center;'>{item.Quantity} шт.</td>                                     
                    <td style='padding: 8px; border-bottom: 1px solid #ddd; text-align: right;'>{itemTotal:N2} ₸</td>                                         
                </tr>");                                                                                                                                      
            }                                                                                                                                                 
                                                                                                                                                              
            var subject = $"Чек об оплате заказа #{order.Id} — RayFluxMarket";                                                                                
            var html = $@"                                                                                                                                    
            <div style='font-family: Arial, sans-serif; padding: 20px; color: #333; max-width: 600px;'>                                                       
                <h2 style='color: #16a34a;'>Оплата прошла успешно! 🎉</h2>                                                                                    
                <p>Здравствуйте, <b>{user.Email}</b>!</p>                                                                                                     
                <p>Благодарим за покупку в магазине <b>RayFluxMarket</b>. Ваш заказ <b>#{order.Id}</b> передан в обработку.</p>                               
                                                                                                                                                              
                <h3 style='margin-top: 20px;'>Детали заказа:</h3>                                                                                             
                <table style='width: 100%; border-collapse: collapse; margin-top: 10px;'>                                                                     
                    <thead>                                                                                                                                   
                        <tr style='background-color: #f3f4f6;'>                                                                                               
                            <th style='padding: 8px; text-align: left;'>Товар</th>                                                                            
                            <th style='padding: 8px; text-align: center;'>Кол-во</th>                                                                         
                            <th style='padding: 8px; text-align: right;'>Сумма</th>                                                                           
                        </tr>                                                                                                                                 
                    </thead>                                                                                                                                  
                    <tbody>                                                                                                                                   
                        {itemsHtml}                                                                                                                           
                    </tbody>                                                                                                                                  
                </table>                                                                                                                                      
                                                                                                                                                              
                <div style='margin-top: 15px; text-align: right; font-size: 18px; font-weight: bold;'>                                                        
                    Итого оплачено: <span style='color: #16a34a;'>{totalAmount:N2} ₸</span>                                                                   
                </div>                                                                                                                                        
                                                                                                                                                              
                <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>                                                                        
                <p style='font-size: 12px; color: #777;'>Это автоматический чек. Если у вас возникли вопросы, свяжитесь со службой поддержки.</p>             
            </div>";                                                                                                                                          
                                                                                                                                                              
            return SendEmailAsync(user.Email, subject, html);                                                                                                 
        }                                                                                                                                                                           
                                                                                                                                                              
        public Task SendPasswordResetAsync(User user, string resetCode)
        {
            var subject = "Сброс пароля — RayFluxMarket";
            var html = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                    <h2 style='color: #4f46e5;'>Сброс пароля</h2>
                    <p>Вы запросили сброс пароля для вашего аккаунта <b>{user.Email}</b>.</p>
                    <p>Ваш одноразовый код для сброса пароля:</p>
                    <div style='background-color: #f3f4f6; padding: 12px; font-size: 18px; font-weight: bold; font-family: monospace; letter-spacing: 2px; text-align: center; margin: 15px 0;'>
                        {resetCode}
                    </div>
                    <p>Код действителен в течение 1 часа.</p>
                    <p>Если вы не запрашивали сброс пароля, просто проигнорируйте это письмо.</p>
                    <br>
                    <p>С уважением, <br><b>Команда RayFluxMarket</b></p>
                </div>";
            return SendEmailAsync(user.Email, subject, html);
        }

        public Task SendWelcomeAsync(User user)
        {
            var subject = "Добро пожаловать в RayFluxMarket!";
            var html = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                    <h2 style='color: #4f46e5;'>Приветствуем в RayFluxMarket!</h2>
                    <p>Спасибо за регистрацию в нашем интернет-магазине.</p>
                    <p>Ваш аккаунт (<b>{user.Email}</b>) успешно создан, и вы можете приступать к покупкам.</p>
                    <br>
                    <p>С уважением, <br><b>Команда RayFluxMarket</b></p>
                </div>";
            return SendEmailAsync(user.Email, subject, html);
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = htmlMessage };
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            try
            {
                await smtp.ConnectAsync(
                    _emailSettings.SmtpServer,
                    _emailSettings.Port,
                    MailKit.Security.SecureSocketOptions.StartTls
                );
                await smtp.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password);
                await smtp.SendAsync(email);
            }
            finally
            {
                // Если ConnectAsync упал, клиент не подключён — не маскируем исходную ошибку
                if (smtp.IsConnected)
                {
                    await smtp.DisconnectAsync(true);
                }
            }
        }
    }
}
