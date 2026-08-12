namespace HorseRacing.Dtos;

public class ApiResult<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }

    public static ApiResult<T> Ok(T data, string message = "")
    {
        return new ApiResult<T> { Success = true, Data = data, Message = message };
    }

    public static ApiResult<T> Fail(string message)
    {
        return new ApiResult<T> { Success = false, Message = message };
    }
}
