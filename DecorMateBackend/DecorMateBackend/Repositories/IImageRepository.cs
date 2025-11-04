using DecorMateBackend.Models;
using DecorMateBackend.Models.DTOs;

namespace DecorMateBackend.Repositories
{
    public interface IImageRepository
    {
        void AddImage(GeneratedImage generatedImage);

        Task<(IEnumerable<GeneratedImageDto>, PaginationMetadata)> GetImagesAsync(string userId , int pageNumber, int pageSize);

        Task <GeneratedImage> GetImageById(int id , CancellationToken cancellationToken);

        void RemoveImage(GeneratedImage generatedImage);
    }
}
