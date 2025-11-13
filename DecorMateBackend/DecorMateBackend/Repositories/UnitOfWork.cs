using DecorMate_Backend_Web_app.Data;
using DecorMate_Backend_Web_app.Models;
using Microsoft.AspNetCore.Identity;

namespace DecorMateBackend.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        public IUserRepository Users { get; }
        public IImageRepository Images { get; }
        public IVendorRepository Vendors { get; }

        public IRefreshTokenRepository RefreshTokens { get; }
        public IEmailConfirmationRepository EmailConfirmations { get; }


        public UnitOfWork(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;

            Users = new UserRepository(_context, _userManager, _signInManager);
            Images = new ImageRepository(_context);
            Vendors = new VendorRepository(_context);
            RefreshTokens = new RefreshTokenRepository(_context);
            EmailConfirmations = new EmailConfirmationRepository(_context);

        }
        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
