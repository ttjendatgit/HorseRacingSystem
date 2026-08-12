using HorseRacing.Dtos;
using Microsoft.AspNetCore.Http;

namespace HorseRacing.Services;

public class ServiceResult<T>
{
    public int StatusCode { get; set; }
    public ApiResult<T> Result { get; set; }
    public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;

    public ServiceResult() { }

    public ServiceResult(int statusCode, ApiResult<T> result)
    {
        StatusCode = statusCode;
        Result = result;
    }

    public static ServiceResult<T> Ok(T data)
    {
        return new ServiceResult<T>(StatusCodes.Status200OK, ApiResult<T>.Ok(data));
    }

    public static ServiceResult<T> Fail(int statusCode, string message)
    {
        return new ServiceResult<T>(statusCode, ApiResult<T>.Fail(message));
    }

    public static ServiceResult<T> Success(T data, int statusCode = StatusCodes.Status200OK)
    {
        return new ServiceResult<T>(statusCode, ApiResult<T>.Ok(data));
    }

    public static ServiceResult<T> Error(string message, int statusCode = StatusCodes.Status500InternalServerError)
    {
        return new ServiceResult<T>(statusCode, ApiResult<T>.Fail(message));
    }
}
