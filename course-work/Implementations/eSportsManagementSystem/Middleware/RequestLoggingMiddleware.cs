using System.Text;
using Microsoft.AspNetCore.Http.Extensions;

namespace eSportsManagementSystem.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Log the request
            var request = context.Request;
            var requestBodyText = string.Empty;

            // Only log request body for specific content types
            if (IsTextBasedContentType(request.ContentType))
            {
                request.EnableBuffering();
                using (var reader = new StreamReader(request.Body, Encoding.UTF8, true, 1024, true))
                {
                    requestBodyText = await reader.ReadToEndAsync();
                    request.Body.Position = 0;  // Reset the position to allow reading again
                }
            }            _logger.LogInformation(
                "Request Details:\nMethod: {Method}\nURL: {Url}\nContent-Type: {ContentType}\nContent-Length: {ContentLength}\nHeaders: {Headers}\nBody: {Body}",
                request.Method,
                request.GetDisplayUrl(),
                request.ContentType,
                request.ContentLength,
                string.Join(", ", request.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}")),
                requestBodyText
            );

            // Capture the response
            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            await _next(context);

            // Log the response
            responseBody.Seek(0, SeekOrigin.Begin);
            var responseBodyText = await new StreamReader(responseBody).ReadToEndAsync();
            responseBody.Seek(0, SeekOrigin.Begin);            _logger.LogInformation(
                "Response Details:\nStatus Code: {StatusCode}\nContent-Type: {ContentType}\nContent-Length: {ContentLength}\nHeaders: {Headers}\nBody: {Body}",
                context.Response.StatusCode,
                context.Response.ContentType,
                context.Response.ContentLength,
                string.Join(", ", context.Response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}")),
                responseBodyText
            );

            await responseBody.CopyToAsync(originalBodyStream);
        }

        private bool IsTextBasedContentType(string? contentType)
        {
            if (string.IsNullOrEmpty(contentType)) return false;
            
            var textBasedTypes = new[]
            {
                "text/",
                "application/json",
                "application/xml",
                "application/x-www-form-urlencoded"
            };

            return textBasedTypes.Any(t => contentType.StartsWith(t, StringComparison.OrdinalIgnoreCase));
        }
    }
}
