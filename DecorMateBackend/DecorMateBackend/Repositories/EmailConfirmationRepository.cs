using DecorMate_Backend_Web_app.Data;
using DecorMateBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace DecorMateBackend.Repositories
{
    public class EmailConfirmationRepository : IEmailConfirmationRepository
    {
        private readonly ApplicationDbContext _context;

        public EmailConfirmationRepository(ApplicationDbContext context) => _context = context;

        public void AddEmailConfirmation(EmailConfirmation emailConfirmation)
        {
            _context.EmailConfirmations.Add(emailConfirmation);
        }

        public async Task<EmailConfirmation?> GetEmailConfirmationByGuidAsync(Guid confirmationGuid, bool includeUser = false)
        {
            var query = _context.EmailConfirmations.AsQueryable();
            
            if (includeUser)
            {
                query = query.Include(e => e.ApplicationUser);
            }

            return await query.FirstOrDefaultAsync(e => e.ConfirmationGuid == confirmationGuid);
        }

        public async Task MarkAsUsedAsync(int id)
        {
            var confirmation = await _context.EmailConfirmations.FindAsync(id);
            if (confirmation != null)
            {
                confirmation.IsUsed = true;
                confirmation.UsedAt = DateTime.UtcNow;
            }
        }

        public async Task CleanupExpiredConfirmationsAsync()
        {
            var expired = await _context.EmailConfirmations
                .Where(e => e.IsExpired || e.IsUsed)
                .ToListAsync();

            _context.EmailConfirmations.RemoveRange(expired);
        }
    }
}

