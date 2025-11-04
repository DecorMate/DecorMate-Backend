using DecorMate_Backend_Web_app.Data;
using DecorMateBackend.Models;
using DecorMateBackend.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace DecorMateBackend.Repositories
{
    public class ImageRepository : IImageRepository
    {
        private readonly ApplicationDbContext _context; 
        public ImageRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public void AddImage(GeneratedImage generatedImage)
        {
            _context.GeneratedImages.Add(generatedImage);
        }
        public async Task<(IEnumerable<GeneratedImageDto>, PaginationMetadata)> GetImagesAsync(string userId, int pageNumber, int pageSize)
        {
            var query = _context.GeneratedImages
                .Where(img => img.ApplicationUserId == userId)
                .OrderByDescending(img => img.CreatedAt);
            var totalItemCount = await query.CountAsync();
            var images = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(img => new GeneratedImageDto
                {
                    Id = img.Id,
                    Url = img.ImageUrl,
                    PublicId = img.CloudinaryPublicId,
                    Prompt = img.Prompt,
                    Title = img.ProjectTitle,
                    CreatedAt = img.CreatedAt
                })
                .ToListAsync();
            var paginationMetadata = new PaginationMetadata(totalItemCount, pageSize, pageNumber);
            return (images, paginationMetadata);
        }

        public async Task<GeneratedImage> GetImageById(int id, CancellationToken cancellationToken)
        {
            return await _context.GeneratedImages
                .FirstOrDefaultAsync(img => img.Id == id, cancellationToken);
        }

        public void RemoveImage(GeneratedImage generatedImage)
        {
            _context.GeneratedImages.Remove(generatedImage);
        }
    }
}
