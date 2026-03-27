using FluentValidation;
using MeshBoard.Contracts.Favorites;

namespace MeshBoard.Api.Middlewares.Validation;

internal sealed class SaveFavoriteNodeRequestValidator : AbstractValidator<SaveFavoriteNodeRequest>
{
    public SaveFavoriteNodeRequestValidator()
    {
        RuleFor(x => x.NodeId).NotEmpty();
    }
}
