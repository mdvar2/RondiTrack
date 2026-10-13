using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using RondiTrack.Domain.Exceptions;
using System.Text.Json;

namespace RondiTrack.Filters;

public class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _serviceProvider;

    public ValidationFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            // Binding errors can echo submitted values; keep the client detail fixed.
            throw new RequestValidationException("The request body is invalid.");
        }

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var validatorType =
                typeof(IValidator<>).MakeGenericType(
                    argument.GetType());

            var validator =
                _serviceProvider.GetService(validatorType)
                    as IValidator;

            if (validator is null)
                continue;

            var validationContext =
                new ValidationContext<object>(argument);

            var result =
                await validator.ValidateAsync(
                    validationContext);

            if (!result.IsValid)
            {
                var message = string.Join(
                    " ",
                    result.Errors.Select(
                        error => error.ErrorMessage));

                var errors = result.Errors
                    .GroupBy(error => string.Join('.', error.PropertyName.Split('.')
                        .Select(JsonNamingPolicy.CamelCase.ConvertName)))
                    .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
                // Preserve the established detail text for existing clients/tests, while adding form field errors.
                throw new RequestValidationException(message, errors);
            }
        }

        await next();
    }
}
