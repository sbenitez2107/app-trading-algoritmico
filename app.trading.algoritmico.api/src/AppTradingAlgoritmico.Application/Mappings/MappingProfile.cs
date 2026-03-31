using AutoMapper;
using AppTradingAlgoritmico.Application.DTOs.Users;
using AppTradingAlgoritmico.Domain.Entities;

namespace AppTradingAlgoritmico.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<ApplicationUser, UserDto>()
            .ForMember(dest => dest.Roles, opt => opt.Ignore()); // roles injected separately
    }
}
