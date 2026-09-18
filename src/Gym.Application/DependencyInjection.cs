using FluentValidation;

using Gym.Application.Auth.ChangePassword;
using Gym.Application.Auth.GetCurrentUser;
using Gym.Application.Auth.Login;
using Gym.Application.Auth.Logout;
using Gym.Application.Auth.Refresh;
using Gym.Application.Members.CreateMember;
using Gym.Application.Members.GetMember;
using Gym.Application.Members.ListMembers;
using Gym.Application.Members.SetMemberActive;
using Gym.Application.Members.UpdateMember;
using Gym.Application.Staff.CreateStaff;
using Gym.Application.Staff.GetStaff;
using Gym.Application.Staff.ListStaff;
using Gym.Application.Staff.ResetStaffPassword;
using Gym.Application.Staff.SetStaffActive;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gym.Application;

/// <summary>
/// The one entry point Gym.Api uses to wire up the use-case layer, so <c>Program.cs</c> names
/// layers rather than individual services.
/// </summary>
/// <remarks>
/// Deliberately thin today: handlers are plain injected classes and the first of them arrives
/// in Phase 2. The seam exists now so that adding a use case later means editing this layer,
/// never the composition root.
/// </remarks>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Use cases need a clock, because CLAUDE.md forbids DateTime.UtcNow: business dates
        // such as "is this subscription expired today" must be steerable from a test.
        // AddInfrastructure registers the same singleton for the audit interceptor, so both
        // use TryAdd: each layer declares the dependency it actually has, and whichever runs
        // first wins without the other throwing or silently replacing it.
        services.TryAddSingleton(TimeProvider.System);

        // One scan instead of a registration line per validator: a new
        // Application/<Feature>/<UseCase>/Validator.cs is picked up by simply existing.
        // Forgetting a registration line would not fail the build — it would silently disable
        // validation for that endpoint, which is the worst possible way to find out. Each
        // validator is registered as IValidator<TCommand>, which is what ValidationFilter<T>
        // in Gym.Api resolves.
        services.AddValidatorsFromAssembly(Common.AssemblyReference.Assembly, includeInternalTypes: true);

        // Handlers are plain classes resolved by type, one line each (CLAUDE.md: no MediatR).
        // Scoped, because they depend on scoped services such as IAppDbContext.
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<ChangePasswordHandler>();
        services.AddScoped<GetCurrentUserHandler>();

        services.AddScoped<CreateMemberHandler>();
        services.AddScoped<UpdateMemberHandler>();
        services.AddScoped<GetMemberHandler>();
        services.AddScoped<ListMembersHandler>();
        services.AddScoped<SetMemberActiveHandler>();

        services.AddScoped<CreateStaffHandler>();
        services.AddScoped<ListStaffHandler>();
        services.AddScoped<GetStaffHandler>();
        services.AddScoped<SetStaffActiveHandler>();
        services.AddScoped<ResetStaffPasswordHandler>();

        return services;
    }
}
