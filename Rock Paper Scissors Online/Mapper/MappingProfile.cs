using Rock_Paper_Scissors_Online.Models;
using AutoMapper;
using Rock_Paper_Scissors_Online.DTOs;
namespace Rock_Paper_Scissors_Online.Mapper
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // User -> UserDto (entity dùng DateTimeOffset; DTO dùng DateTime)
            CreateMap<User, UserDto>()
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt.UtcDateTime))
                .ForMember(dest => dest.LastPlayedAt, opt => opt.MapFrom(src =>
                    src.LastPlayedAt.HasValue ? src.LastPlayedAt.Value.UtcDateTime : (DateTime?)null));

            CreateMap<RegisterDto, User>()
                .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src =>
                    string.IsNullOrWhiteSpace(src.DisplayName) ? src.Username : src.DisplayName))
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.NewGuid()))
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
                .ForMember(dest => dest.Points, opt => opt.MapFrom(src => 1000))
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => "User"))
                .ForMember(dest => dest.TotalGames, opt => opt.MapFrom(src => 0))
                .ForMember(dest => dest.Wins, opt => opt.MapFrom(src => 0))
                .ForMember(dest => dest.Losses, opt => opt.MapFrom(src => 0))
                .ForMember(dest => dest.Ties, opt => opt.MapFrom(src => 0))
                .ForMember(dest => dest.CurrentWinStreak, opt => opt.MapFrom(src => 0))
                .ForMember(dest => dest.LongestWinStreak, opt => opt.MapFrom(src => 0))
                .ForMember(dest => dest.Avatar, opt => opt.MapFrom(src => 1))
                .ForMember(dest => dest.LastPlayedAt, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.HistoryCreatorUsers, opt => opt.Ignore())
                .ForMember(dest => dest.HistoryOpponents, opt => opt.Ignore())
                .ForMember(dest => dest.PointTransactions, opt => opt.Ignore())
                .ForMember(dest => dest.RefreshTokens, opt => opt.Ignore());

            CreateMap<User, AuthResponseDto>()
                .ForMember(dest => dest.User, opt => opt.MapFrom(src => src))
                .ForMember(dest => dest.Token, opt => opt.Ignore())
                .ForMember(dest => dest.RefreshToken, opt => opt.Ignore())
                .ForMember(dest => dest.ExpiresAt, opt => opt.Ignore());

            // Room mappings (runtime game room, not EF Room entity)
            CreateMap<GameRoom, RoomResponseDto>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString().ToLower()));
            CreateMap<RoomPlayer, PlayerDto>().ReverseMap();
        }
    }
}