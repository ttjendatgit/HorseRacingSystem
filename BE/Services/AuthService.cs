using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace HorseRacing.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IOwnerRepository _owners;
    private readonly IJockeyRepository _jockeys;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtTokenService _jwtTokenService;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthService(
        IUserRepository users,
        IOwnerRepository owners,
        IJockeyRepository jockeys,
        IUnitOfWork unitOfWork,
        JwtTokenService jwtTokenService)
    {
        _users = users;
        _owners = owners;
        _jockeys = jockeys;
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<ServiceResult<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        request.Email = request.Email.Trim();

        if (request.Role is not (UserRole.HorseOwner or UserRole.Jockey or UserRole.Spectator))
        {
            return ServiceResult<AuthResponse>.Fail(StatusCodes.Status400BadRequest, "Vai trò không được hỗ trợ");
        }

        var exists = await _users.EmailExistsAsync(request.Email);
        if (exists)
        {
            return ServiceResult<AuthResponse>.Fail(StatusCodes.Status409Conflict, "Email đã tồn tại");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            Role = request.Role,
            FullName = request.FullName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await _users.AddAsync(user);

        var now = DateTime.UtcNow;

        if (request.Role == UserRole.HorseOwner)
        {
            var owner = new Owner
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                OwnerCode = GenerateOwnerCode(),
                OwnerType = "Cá nhân",
                Phone = request.Phone,
                Address = request.Address,
                JoinDate = now,
                Status = "Đang hoạt động",
                CreatedAt = now,
                UpdatedAt = now
            };
            await _owners.AddAsync(owner);
        }

        if (request.Role == UserRole.Jockey)
        {
            var jockey = new Jockey
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                LicenseNumber = request.LicenseNumber,
                LicenseFile = request.LicenseFile,
                Phone = request.Phone,
                Address = request.Address,
                DateOfBirth = request.DateOfBirth,
                Height = request.Height,
                Weight = request.Weight,
                IdCardNumber = request.IdCardNumber,
                ApprovalStatus = ApprovalStatus.Pending,
                Status = "Đang hoạt động",
                CreatedAt = now,
                UpdatedAt = now
            };
            await _jockeys.AddAsync(jockey);

            // Jockeys also get an Owner profile so they can own horses
            var jockeyOwner = new Owner
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                OwnerCode = GenerateOwnerCode(),
                OwnerType = "Cá nhân",
                Phone = request.Phone,
                Address = request.Address,
                JoinDate = now,
                Status = "Đang hoạt động",
                CreatedAt = now,
                UpdatedAt = now
            };
            await _owners.AddAsync(jockeyOwner);
        }

        await _unitOfWork.SaveChangesAsync();

        var token = _jwtTokenService.CreateToken(user);
        var refreshToken = _jwtTokenService.CreateRefreshToken();
        user.RefreshToken = HashToken(refreshToken);
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        var response = new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role,
            Token = token,
            RefreshToken = refreshToken // Return raw token to client
        };

        return ServiceResult<AuthResponse>.Ok(response);
    }

    public async Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            return ServiceResult<AuthResponse>.Fail(StatusCodes.Status401Unauthorized, "Thông tin đăng nhập không chính xác");
        }

        var user = await _users.GetByEmailAsync(email);
        if (user == null)
        {
            return ServiceResult<AuthResponse>.Fail(StatusCodes.Status401Unauthorized, "Thông tin đăng nhập không chính xác");
        }

        var password = request.Password?.Trim() ?? string.Empty;
        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            return ServiceResult<AuthResponse>.Fail(StatusCodes.Status401Unauthorized, "Thông tin đăng nhập không chính xác");
        }

        if (!user.IsActive)
        {
            return ServiceResult<AuthResponse>.Fail(StatusCodes.Status403Forbidden, "Tài khoản đã bị vô hiệu hóa");
        }

        user.LastLoginAt = DateTime.UtcNow;

        var token = _jwtTokenService.CreateToken(user);
        var refreshToken = _jwtTokenService.CreateRefreshToken();
        user.RefreshToken = HashToken(refreshToken);
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        var response = new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role,
            Token = token,
            RefreshToken = refreshToken
        };

        return ServiceResult<AuthResponse>.Ok(response);
    }

    public async Task<ServiceResult<OwnerProfileResponse>> GetOwnerProfileAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user == null)
        {
            return ServiceResult<OwnerProfileResponse>.Fail(
                StatusCodes.Status404NotFound,
                "Không tìm thấy người dùng");
        }

        if (user.Role != UserRole.HorseOwner || user.OwnerProfile == null)
        {
            return ServiceResult<OwnerProfileResponse>.Fail(
                StatusCodes.Status404NotFound,
                "Không tìm thấy hồ sơ chủ sở hữu");
        }

        var owner = user.OwnerProfile;
        return ServiceResult<OwnerProfileResponse>.Ok(new OwnerProfileResponse
        {
            UserId = user.Id,
            OwnerId = owner.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            OwnerCode = owner.OwnerCode,
            OwnerType = owner.OwnerType,
            Status = owner.Status,
            JoinDate = owner.JoinDate,
            HorseCount = owner.Horses.Count
        });
    }

    private static string GenerateOwnerCode() =>
        $"OWN-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    public async Task<ServiceResult<object>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmNewPassword)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Mật khẩu mới không khớp.");
        }

        if (request.NewPassword == request.CurrentPassword)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Mật khẩu mới phải khác mật khẩu hiện tại.");
        }

        var user = await _users.GetByIdAsync(userId);
        if (user == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy người dùng");
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (result == PasswordVerificationResult.Failed)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Mật khẩu hiện tại không đúng.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult<object>.Ok(new { message = "Mật khẩu đã được thay đổi thành công." });
    }

    public async Task<ServiceResult<object>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy người dùng");
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            user.FullName = request.FullName.Trim();
        }
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            user.PhoneNumber = request.PhoneNumber.Trim();
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult<object>.Ok(new
        {
            message = "Hồ sơ đã được cập nhật thành công.",
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            user.CreatedAt
        });
    }

    public async Task<ServiceResult<object>> ForgotPasswordAsync(string email)
    {
        var user = await _users.GetByEmailAsync(email.Trim());
        if (user == null)
        {
            // Return OK even if email not found to prevent enumeration
            return ServiceResult<object>.Ok(new { message = "Nếu email tồn tại, liên kết đặt lại mật khẩu đã được gửi" });
        }

        var resetToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        user.ResetToken = resetToken;
        user.ResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        await _users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult<object>.Ok(new { message = "Nếu email tồn tại, liên kết đặt lại mật khẩu đã được gửi", resetToken });
    }

    public async Task<ServiceResult<object>> ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmPassword)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Mật khẩu không khớp.");
        }

        var user = await _users.GetByEmailAsync(request.Email.Trim());
        if (user == null || user.ResetToken == null || user.ResetTokenExpiry == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Token không hợp lệ hoặc đã hết hạn.");
        }

        if (user.ResetToken != request.Token || user.ResetTokenExpiry < DateTime.UtcNow)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Token không hợp lệ hoặc đã hết hạn.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.ResetToken = null;
        user.ResetTokenExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult<object>.Ok(new { message = "Mật khẩu đã được đặt lại thành công." });
    }

    public async Task<ServiceResult<AuthResponse>> RefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return ServiceResult<AuthResponse>.Fail(StatusCodes.Status400BadRequest, "Cần có refresh token");

        var hash = HashToken(refreshToken);
        var user = await _users.GetByRefreshTokenHashAsync(hash);

        if (user == null || user.RefreshTokenExpiry == null || user.RefreshTokenExpiry < DateTime.UtcNow)
            return ServiceResult<AuthResponse>.Fail(StatusCodes.Status401Unauthorized, "Refresh token không hợp lệ hoặc đã hết hạn");

        var newToken = _jwtTokenService.CreateToken(user);
        var newRefreshToken = _jwtTokenService.CreateRefreshToken();
        user.RefreshToken = HashToken(newRefreshToken);
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult<AuthResponse>.Ok(new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role,
            Token = newToken,
            RefreshToken = newRefreshToken
        });
    }

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }
}
