namespace MeshBoard.Api.Middlewares.Validation;

internal static class ValidationExtensions
{
    public static RouteHandlerBuilder WithValidator<T>(this RouteHandlerBuilder builder) where T : class
    {
        return builder.AddEndpointFilter<ValidationFilter<T>>();
    }
}
