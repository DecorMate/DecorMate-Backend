using DecorMateBackend.Models;
namespace DecorMateBackend.Repositories
{
    public interface IVendorRepository
    {
        Task <VendorRating> GetVendorRateByUserAsync (string vendorId, string userId);

        Task AddRatingAsync(VendorRating rating);    

        Task <(double , int)> GetAverageRatingAndTotalRatingsAsync(string vendorId);

        void UpdateRating(VendorRating rating);

        int CountRatings(string vendorId);

        double GetAverageRatingsOfVendor(string vendorId);

    }
}
