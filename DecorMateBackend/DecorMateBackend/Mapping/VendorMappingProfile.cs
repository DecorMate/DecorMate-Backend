using AutoMapper;
using DecorMate_Backend_Web_app.Models;
using DecorMateBackend.Models.DTOs;

namespace DecorMateBackend.Mapping
{
    public class VendorMappingProfile : Profile
    {
        public VendorMappingProfile()
        {
            CreateMap<ApplicationUser, VendorDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
                .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.FirstName))
                .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.LastName))
                .ForMember(dest => dest.CompanyName, opt => opt.MapFrom(src => src.CompanyName))
                .ForMember(dest => dest.ProfilePictureUrl, opt => opt.MapFrom(src => src.ProfilePictureUrl))
                .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber))
                .ForMember(dest => dest.Location, opt => opt.MapFrom(src => src.Location))
                .ForMember(dest => dest.Category, opt => opt.MapFrom(src => src.ProfessionalCategory != null ? src.ProfessionalCategory.ToString() : null))
                .ForMember(dest => dest.IsSponsored, opt => opt.MapFrom(src => src.IsSponsored))
                .ForMember(dest => dest.AverageRating, opt => opt.MapFrom(src => src.Rating))
                .ForMember(dest => dest.RatingsCount, opt => opt.MapFrom(src => src.RatingsCount));
        }
    }
}

