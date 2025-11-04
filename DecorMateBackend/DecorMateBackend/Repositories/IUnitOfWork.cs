namespace DecorMateBackend.Repositories
{
    public interface IUnitOfWork : IDisposable
    {
        IUserRepository Users { get;}
        IImageRepository Images { get; }
        IVendorRepository Vendors { get; }
        IRefreshTokenRepository RefreshTokens { get; }

        Task<int> SaveChangesAsync();
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
