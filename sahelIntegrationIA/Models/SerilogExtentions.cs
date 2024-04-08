using Serilog;
using ILogger = Serilog.ILogger;

namespace sahelIntegrationIA.Models
{
    public static class SerilogExtentions
    {
        public static ILogger LogInformation(this ILogger logger, string message, string requestId = "", string key = "", params object?[]? propertyValues)
        {
            logger.Information(message, propertyValues);
            return logger;
        }

        public static ILogger LogException(this ILogger logger, Exception exception, string message = "", string requestId = "", string key = "", params object?[]? propertyValues)
        {
            logger.Error(exception, message, propertyValues);
            return logger;
        }
    }
}
