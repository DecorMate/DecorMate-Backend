namespace DecorMateBackend.Repositories
{
    public interface IEmailConfirmationRepository
    {
        void AddEmailConfirmation(Models.EmailConfirmation emailConfirmation);
        Task<Models.EmailConfirmation?> GetEmailConfirmationByGuidAsync(Guid confirmationGuid, bool includeUser = false);
        Task MarkAsUsedAsync(int id);
        Task CleanupExpiredConfirmationsAsync();
    }
}

