using System;
using System.Collections.Generic;
using System.Text;

namespace PromVesServer.Service
{
    //если предстоить передавать какие нибуь обьекты, например обьект класса User
    public class ServiceResult<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }

        public static ServiceResult<T> Ok(T data) =>
            new() { Success = true, Data = data };

        public static ServiceResult<T> Fail(string message) =>
            new() { Success = false, Message = message };
    }
}
