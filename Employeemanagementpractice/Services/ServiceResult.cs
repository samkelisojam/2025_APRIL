namespace Employeemanagementpractice.Services
{
    public class ServiceResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorField { get; set; }
        public Dictionary<string, string> ValidationErrors { get; set; } = new();

        public static ServiceResult Ok() => new() { Success = true };
        public static ServiceResult Fail(string message, string? field = null)
            => new() { Success = false, ErrorMessage = message, ErrorField = field };
    }

    public class ServiceResult<T> : ServiceResult
    {
        public T? Data { get; set; }

        public static ServiceResult<T> Ok(T data) => new() { Success = true, Data = data };
        public new static ServiceResult<T> Fail(string message, string? field = null)
            => new() { Success = false, ErrorMessage = message, ErrorField = field };
    }
}
