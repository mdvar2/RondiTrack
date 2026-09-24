using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using RondiTrack.Exceptions;

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
                await validator.ValidateAsync(validationContext);

            if (!result.IsValid)
            {
                var message = string.Join(
                    " ",
                    result.Errors.Select(
                        error => error.ErrorMessage));

                throw new RequestValidationException(message);
            }
        }

        await next();
    }
}