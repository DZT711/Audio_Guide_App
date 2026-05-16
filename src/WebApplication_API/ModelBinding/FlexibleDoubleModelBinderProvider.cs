using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace WebApplication_API.ModelBinding;

public sealed class FlexibleDoubleModelBinderProvider : IModelBinderProvider
{
    private static readonly IModelBinder Binder = new FlexibleDoubleModelBinder();

    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var modelType = context.Metadata.ModelType;
        return modelType == typeof(double) || modelType == typeof(double?)
            ? Binder
            : null;
    }
}
