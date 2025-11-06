using DecorMate_Backend_Web_app.Data;
using DecorMateBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace DecorMateBackend.Repositories
{
    public class VendorRepository : IVendorRepository
    {
        private readonly ApplicationDbContext _context;
        public VendorRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<VendorRating> GetVendorRateByUserAsync(string vendorId, string userId)
        {
            return await _context.VendorRatings.
                FirstOrDefaultAsync(r => r.ApplicationUserId == vendorId && r.RatedByUserId == userId);
        }

        public async Task AddRatingAsync(VendorRating rating)
        {
            await _context.VendorRatings.AddAsync(rating);
        }

        public async Task<(double, int)> GetAverageRatingAndTotalRatingsAsync(string vendorId)
        {
            var ratings = await _context.VendorRatings
                .Where(r => r.ApplicationUserId == vendorId)
                .ToListAsync();
            if (ratings.Count == 0)
            {
                return (0, 0);
            }
            double averageRating = ratings.Average(r => r.Score);
            int totalRatings = ratings.Count;
            return (averageRating, totalRatings);
        }

        public void UpdateRating(VendorRating rating)
        {
            _context.VendorRatings.Update(rating);
        }

        public int CountRatings(string vendorId)
        {
            return _context.VendorRatings.Count(r => r.ApplicationUserId == vendorId);
        }

        public double GetAverageRatingsOfVendor(string vendorId)
        {
            return _context.VendorRatings
            .Where(r => r.ApplicationUserId == vendorId)
            .Select(r => (double?)r.Score)
            .Average() ?? 0.0;
        }

    }
}
