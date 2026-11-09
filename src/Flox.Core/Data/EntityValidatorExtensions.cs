using FluentValidation;

namespace Flox.Core.Data;

public static class EntityValidatorExtensions
{
    private static IEnumerable<string> PrivateValidateObject<T>(this IValidator<T> validator, T arg)
    {
        var result = validator.Validate(arg);
        if (result.IsValid)
        {
            return [];
        }

        return result.Errors.Select(e => e.ErrorMessage);
    }

    private static async Task<IEnumerable<string>> PrivateValidateObjectAsync<T>(this IValidator<T> validator, T arg)
    {
        var result = await validator.ValidateAsync(arg);
        if (result.IsValid)
        {
            return [];
        }

        return result.Errors.Select(e => e.ErrorMessage);
    }

    private static IEnumerable<string> PrivateValidateObjectMember<T>(this IValidator<T> validator, T arg, string propertyName)
    {
        var result = validator.Validate(arg,
                                        options =>
                                        {
                                            options.IncludeProperties(propertyName);
                                        });
        if (result.IsValid)
        {
            return [];
        }

        return result.Errors.Select(e => e.ErrorMessage);
    }

    private static async Task<IEnumerable<string>> PrivateValidateObjectMemberAsync<T>(this IValidator<T> validator, T arg, string propertyName)
    {
        var result = await validator.ValidateAsync(arg,
                                                   options =>
                                                   {
                                                       options.IncludeProperties(propertyName);
                                                   });
        if (result.IsValid)
        {
            return [];
        }

        return result.Errors.Select(e => e.ErrorMessage);
    }

    public static Func<T, IEnumerable<string>> FormValidator<T>(this IValidator<T> validator)
    {
        return validator.PrivateValidateObject;
    }

    public static Func<T, Task<IEnumerable<string>>> AsyncFormValidator<T>(this IValidator<T> validator)
    {
        return validator.PrivateValidateObjectAsync;
    }

    public static Func<T, string, IEnumerable<string>> FormMemberValidator<T>(this IValidator<T> validator)
    {
        return validator.PrivateValidateObjectMember;
    }

    public static Func<T, string, Task<IEnumerable<string>>> AsyncFormMemberValidator<T>(this IValidator<T> validator)
    {
        return validator.PrivateValidateObjectMemberAsync;
    }
}
