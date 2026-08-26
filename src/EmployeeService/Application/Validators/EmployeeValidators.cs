using EmployeeService.Application.DTOs;
using FluentValidation;

namespace EmployeeService.Application.Validators;

public class CreateEmployeeValidator : AbstractValidator<CreateEmployeeRequest>
{
    public CreateEmployeeValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Department).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Position).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EmploymentDate).LessThanOrEqualTo(DateTime.UtcNow.AddDays(1));
    }
}

public class UpdateEmployeeValidator : AbstractValidator<UpdateEmployeeRequest>
{
    public UpdateEmployeeValidator()
    {
        RuleFor(x => x.Department).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Position).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EmploymentDate).LessThanOrEqualTo(DateTime.UtcNow.AddDays(1));
    }
}

public class UpdatePreferencesValidator : AbstractValidator<UpdatePreferencesRequest>
{
    public UpdatePreferencesValidator()
    {
        RuleFor(x => x.Preferences).NotNull().NotEmpty();
    }
}
