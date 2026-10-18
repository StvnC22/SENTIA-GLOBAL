using System.Security.Claims;
using AnalisisSentimiento.Domain.Constants;
using AnalisisSentimiento.Infrastructure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AnalisisSentimiento.Web.Endpoints;

public class Users : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapIdentityApi<ApplicationUser>();

        groupBuilder.MapPost(Logout, "logout").RequireAuthorization();
        groupBuilder.MapGet(GetMe, "me").RequireAuthorization();
        groupBuilder.MapGet(GetRolesCatalog, "roles");
    }

    [EndpointSummary("Log out")]
    [EndpointDescription("Logs out the current user by clearing the authentication cookie.")]
    public static async Task<Results<Ok, UnauthorizedHttpResult>> Logout(
        SignInManager<ApplicationUser> signInManager,
        [FromBody] object empty
    )
    {
        if (empty != null)
        {
            await signInManager.SignOutAsync();
            return TypedResults.Ok();
        }

        return TypedResults.Unauthorized();
    }

    [EndpointSummary("Perfil del usuario autenticado")]
    public static async Task<Results<Ok<UserProfileDto>, UnauthorizedHttpResult>> GetMe(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager
    )
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        return TypedResults.Ok(
            new UserProfileDto(
                user.Id,
                user.UserName ?? user.Email ?? user.Id,
                user.Email,
                roles.ToArray()
            )
        );
    }

    [EndpointSummary("Catálogo de roles del Observatorio")]
    public static Ok<IReadOnlyList<RoleInfoDto>> GetRolesCatalog()
    {
        return TypedResults.Ok<IReadOnlyList<RoleInfoDto>>(
            [
                new(Roles.Administrator, "Administrador", "Gestión total, API keys y usuarios."),
                new(Roles.Analyst, "Analista", "Búsquedas, reportes y monitoreos periódicos."),
                new(Roles.Consulta, "Consulta", "Solo lectura del tablero y exportaciones."),
            ]
        );
    }
}

public record UserProfileDto(string Id, string UserName, string? Email, IReadOnlyList<string> Roles);

public record RoleInfoDto(string Name, string DisplayName, string Description);
