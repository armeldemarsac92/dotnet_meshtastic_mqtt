using FluentValidation;
using MeshBoard.Contracts.Configuration;

namespace MeshBoard.Api.Middlewares.Validation;

internal sealed class SaveBrokerPreferenceRequestValidator : AbstractValidator<SaveBrokerPreferenceRequest>
{
    public SaveBrokerPreferenceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Host).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Port).InclusiveBetween(1, 65535);
        RuleFor(x => x.DownlinkTopic).NotEmpty().MaximumLength(500);
    }
}
