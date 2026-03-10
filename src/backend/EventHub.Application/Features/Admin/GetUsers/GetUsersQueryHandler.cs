using EventHub.Application.Abstractions;
using EventHub.Application.Common;
using EventHub.Application.Features.Admin;
using FluentValidation;
using MediatR;

namespace EventHub.Application.Features.Admin.GetUsers;

public record GetUsersQuery(int Page = 1, int PageSize = 50, string? Search = null)
    : IRequest<PagedResult<AdminUserDto>>;

public sealed class GetUsersQueryHandler(IIdentityAdminService identityAdminService)
    : IRequestHandler<GetUsersQuery, PagedResult<AdminUserDto>>
{
    public Task<PagedResult<AdminUserDto>> Handle(
        GetUsersQuery query,
        CancellationToken cancellationToken)
    {
        return identityAdminService.GetUsersAsync(query.Page, query.PageSize, query.Search, cancellationToken);
    }
}

public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 200)
            .WithMessage("PageSize must be between 1 and 200.");
    }
}
