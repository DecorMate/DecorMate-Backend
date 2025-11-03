using DecorMate_Backend_Web_app.Data;
using DecorMateBackend.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
namespace DecorMateBackend.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    { 
        private readonly ApplicationDbContext _context;

        public RefreshTokenRepository(ApplicationDbContext context) => _context = context;

        public void AddRefreshToken(RefreshToken refreshToken)
        {
            _context.RefreshTokens.Add(refreshToken);
        }

        public async Task<RefreshToken> GetRefreshTokenAsync(string refreshToken  , bool IncludeApplicationUser)
        {
            if (IncludeApplicationUser)
                return await _context.RefreshTokens.Include(r => r.ApplicationUser).FirstOrDefaultAsync(r => r.Token == refreshToken);
            return await _context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == refreshToken);
        }

    }
}
