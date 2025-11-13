using AutoMapper;
using DecorMateBackend.Models;
using DecorMateBackend.Models.DTOs;

namespace DecorMateBackend.Mapping
{
    public class ImageMappingProfile : Profile
    {
        public ImageMappingProfile()
        {
            CreateMap<GeneratedImage, GeneratedImageDto>()
                .ForMember(dest => dest.Url, opt => opt.MapFrom(src => src.ImageUrl))
                .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.ProjectTitle))
                .ForMember(dest => dest.PublicId, opt => opt.MapFrom(src => src.CloudinaryPublicId));
        }
    }
}

