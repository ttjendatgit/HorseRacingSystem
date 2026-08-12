using System.Threading.Tasks;
using HorseRacing.Dtos;
using System;

namespace HorseRacing.Services.Interfaces;

public interface IAuthService
{
    Task<ServiceResult<AuthResponse>> RegisterAsync(RegisterRequest request);
    Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request);
    Task<ServiceResult<OwnerProfileResponse>> GetOwnerProfileAsync(Guid userId);
    Task<ServiceResult<object>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task<ServiceResult<object>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
    Task<ServiceResult<object>> ResetPasswordAsync(ResetPasswordRequest request);
    Task<ServiceResult<object>> ForgotPasswordAsync(string email);
    Task<ServiceResult<AuthResponse>> RefreshTokenAsync(string refreshToken);
}
