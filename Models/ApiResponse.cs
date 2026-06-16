namespace InterviewPrepAPI.Models;

public class ApiResponse<T>
{
    public int ResponseCode { get; set; }
    public bool IsSuccess { get; set; }
    public T? Response { get; set; }
    public string ResponseMsg { get; set; } = string.Empty;

    public static ApiResponse<T> Success(T data, string message = "Success", int code = 200)
    {
        return new ApiResponse<T>
        {
            ResponseCode = code,
            IsSuccess = true,
            Response = data,
            ResponseMsg = message
        };
    }

    public static ApiResponse<T> Fail(string message, int code = 400)
    {
        return new ApiResponse<T>
        {
            ResponseCode = code,
            IsSuccess = false,
            Response = default,
            ResponseMsg = message
        };
    }
}

public class ApiResponse
{
    public int ResponseCode { get; set; }
    public bool IsSuccess { get; set; }
    public string ResponseMsg { get; set; } = string.Empty;

    public static ApiResponse Success(string message = "Success", int code = 200)
    {
        return new ApiResponse
        {
            ResponseCode = code,
            IsSuccess = true,
            ResponseMsg = message
        };
    }

    public static ApiResponse Fail(string message, int code = 400)
    {
        return new ApiResponse
        {
            ResponseCode = code,
            IsSuccess = false,
            ResponseMsg = message
        };
    }
}
