using System.Reflection;
using JasperFx.CodeGeneration.Frames;
using Wolverine.Http;
using Wolverine.Http.Resources;

namespace MoreThanCode.AFrameExample.Shared;

public record LazyCreationResponse : IHttpAware
{
    private readonly Lazy<string> _urlCreation;

    public LazyCreationResponse(Func<string> urlCreation) =>
        _urlCreation = new Lazy<string>(urlCreation, LazyThreadSafetyMode.PublicationOnly);

    public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        builder.RemoveStatusCodeResponse(200);

        var create = new MethodCall(method.DeclaringType!, method).Creates.FirstOrDefault()?.VariableType;
        var metadata = new WolverineProducesResponseTypeMetadata { Type = create, StatusCode = 201 };
        builder.Metadata.Add(metadata);
    }

    void IHttpAware.Apply(HttpContext context)
    {
        context.Response.Headers.Location = _urlCreation.Value;
        context.Response.StatusCode = 201;
    }

    public static LazyCreationResponse<T> For<T>(Func<string> urlCreation, Func<T> value) => new(urlCreation, value);
}

public record LazyCreationResponse<T> : LazyCreationResponse
{
    private readonly Lazy<T> _lazyValue;

    public LazyCreationResponse(Func<string> urlCreation, Func<T> value) : base(urlCreation) =>
        _lazyValue = new(value, LazyThreadSafetyMode.PublicationOnly);

    public T Value => _lazyValue.Value;
}