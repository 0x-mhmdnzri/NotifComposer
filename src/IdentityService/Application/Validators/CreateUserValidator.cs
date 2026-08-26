using FluentValidation;
using IdentityService.Application.DTOs;

namespace IdentityService.Application.Validators;

public class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("FullName is required")
            .MaximumLength(200);

        RuleFor(x => x.Mobile)
            .NotEmpty().WithMessage("Mobile is required")
            .Matches(@"^09\d{9}$").WithMessage("Mobile must be a valid Iranian mobile number (09xxxxxxxxx)");
    }
}
