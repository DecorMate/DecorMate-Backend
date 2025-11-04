namespace DecorMateBackend.Repositories
{
    public interface IRefreshTokenRepository
    {
        void AddRefreshToken(RefreshToken refreshToken);
        Task<RefreshToken> GetRefreshTokenAsync(string refreshToken, bool IncludeApplicationUser);
    }
}
