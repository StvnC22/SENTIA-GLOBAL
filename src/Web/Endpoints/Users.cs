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
        groupBuilder.MapPut(SetRole, "{userId}/role").RequireAuthorization("ManageUsers");
    }

    [EndpointSummary("Asignar rol a un usuario")]
    public static async Task<Results<Ok, NotFound, BadRequest<string>>> SetRole(
        string userId,
        string role,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager
    )
    {
        if (!new[] { Roles.Administrator, Roles.Analyst, Roles.Viewer }.Contains(role))
            return TypedResults.BadRequest("Rol no permitido.");

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return TypedResults.NotFound();

        if (!await roleManager.RoleExistsAsync(role))
            return TypedResults.BadRequest("El rol no está configurado.");

        var currentRoles = await userManager.GetRolesAsync(user);
        var removed = await userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!removed.Succeeded)
            return TypedResults.BadRequest("No se pudieron actualizar los roles.");

        var added = await userManager.AddToRoleAsync(user, role);
        return added.Succeeded
            ? TypedResults.Ok()
            : TypedResults.BadRequest("No se pudo asignar el rol.");
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
}
