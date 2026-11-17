using System.Security.Claims;                                                                                                                                 
                                                                                                                                                              
namespace RayFluxMarket.Extensions                                                                                                                            
{                                                                                                                                                             
    public static class ClaimsPrincipalExtensions                                                                                                             
    {                                                                                                                                                         
        // Возвращает ID текущего пользователя из claim NameIdentifier.                                                                                       
        // Бросает UnauthorizedAccessException, если claim отсутствует или не парсится.                                                                       
        public static int GetUserId(this ClaimsPrincipal user)                                                                                                
        {                                                                                                                                                     
            var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;                                                                                 
                                                                                                                                                              
            if (string.IsNullOrEmpty(userIdStr))                                                                                                              
            {                                                                                                                                                 
                throw new UnauthorizedAccessException("ID пользователя не найден в токене.");                                                                 
            }                                                                                                                                                 
                                                                                                                                                              
            if (!int.TryParse(userIdStr, out int userId))                                                                                                     
            {                                                                                                                                                 
                throw new UnauthorizedAccessException("ID пользователя в токене имеет неверный формат.");                                                     
            }                                                                                                                                                 
                                                                                                                                                              
            return userId;                                                                                                                                    
        }                                                                                                                                                     
    }                                                                                                                                                         
}     