namespace RedisSample.Shared.Features.Identity.Dtos;

public partial class SignInResponseDto : TokenResponseDto
{
    public bool RequiresTwoFactor { get; set; }
}
