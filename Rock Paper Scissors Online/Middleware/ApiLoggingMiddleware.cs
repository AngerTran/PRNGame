namespace Rock_Paper_Scissors_Online.Middleware
{
    public class ApiLoggingMiddleware
    {
        private readonly RequestDelegate _next;

        public ApiLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Only log API calls (not SignalR or static files)
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                var method = context.Request.Method;
                var path = context.Request.Path;
                var queryString = context.Request.QueryString.ToString();

                Console.WriteLine($"\u001b[33m[API]\u001b[0m {method} {path}{queryString}");
            }

            await _next(context);
        }
    }
}
