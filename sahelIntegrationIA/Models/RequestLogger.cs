using eServicesV2.Kernel.Core;
using eServicesV2.Kernel.Core.InstanseScopeTypes;
using eServicesV2.Kernel.Core.Logging;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace sahelIntegrationIA.Models
{
    public class RequestLogger : IScoped, IRequestLogger
    {
        private readonly Serilog.ILogger _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public RequestLogger(Serilog.ILogger logger, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public void LogInformation(string message, params object?[]? propertyValues)
        {
            _logger.LogInformation(
                message: message,
                propertyValues: propertyValues
               );
        }

        public void LogException(Exception exception, string message = "", params object?[]? propertyValues)
        {
            _logger.LogException(
                exception, message: message,
                propertyValues: propertyValues
               );
        }
    }

}
