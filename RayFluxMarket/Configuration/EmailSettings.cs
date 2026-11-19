using System.ComponentModel.DataAnnotations;

namespace RayFluxMarket.Configuration
{
    public class EmailSettings
    {
        public const string SectionName = "EmailSettings";

        [Required]
        public string SmtpServer { get; set; } = string.Empty;

        [Range(1, 65535)]
        public int Port { get; set; } = 587;

        [Required]
        public string SenderName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string SenderEmail { get; set; } = string.Empty;

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
