using AutoMapper;
using DecorMate_Backend_Web_app.Models;
using DecorMate_Backend_Web_app.Models.DTOs;
using DecorMateBackend.Models.Enums;
using DecorMateBackend.Models.DTOs;

namespace DecorMateBackend.Mapping
{
    public class AccountMappingProfile : Profile
    {
        public AccountMappingProfile()
        {
            CreateMap<RegisterDto, ApplicationUser>()
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.Email))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
                .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.FirstName))
                .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.LastName))
                .ForMember(dest => dest.EmailConfirmed, opt => opt.MapFrom(_ => false))
                .ForMember(dest => dest.Provider, opt => opt.MapFrom(_ => AuthProvider.Local));

            CreateMap<ApplicationUser, UserDto>();
        }
    }
}

