using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace WebApplication_API.ModelBinding;

public sealed class FlexibleDoubleModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueResult);
        var rawValue = valueResult.FirstValue;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            if (bindingContext.ModelType == typeof(double?))
            {
                bindingContext.Result = ModelBindingResult.Success(null);
            }

            return Task.CompletedTask;
        }

        if (TryParse(rawValue, out var parsedValue))
        {
            bindingContext.Result = ModelBindingResult.Success(parsedValue);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName,
            $"The value '{rawValue}' is not a valid number.");

        return Task.CompletedTask;
    }

    private static bool TryParse(string value, out double parsedValue) =>
        double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out parsedValue)
        || double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out parsedValue);
}
