using System.Security.Claims;
using AnalisisSentimiento.Application.Monitoring.Commands.LogActivity;
using AnalisisSentimiento.Domain.Constants;
using AnalisisSentimiento.Infrastructure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AnalisisSentimiento.Web.Endpoints;

public class Users : IEndpointGroup
{
    private static readonly string[] AssignableRoles =
    [
        Roles.Administrator,
        Roles.Analyst,
        Roles.Consulta,
    ];

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapIdentityApi<ApplicationUser>();

        groupBuilder.MapPost(Logout, "logout").RequireAuthorization();
        groupBuilder.MapGet(GetMe, "me").RequireAuthorization();
        groupBuilder.MapGet(GetRolesCatalog, "roles");

        var admin = groupBuilder
            .MapGroup("admin")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Administrator));
        admin.MapGet(ListUsers, "users");
        admin.MapPost(CreateUser, "users");
        admin.MapPut(ChangeUserRole, "users/{id}/role");
        admin.MapPost(UnlockUser, "users/{id}/unlock");
        admin.MapDelete(DeleteUser, "users/{id}");
    }

    [EndpointSummary("Log out")]
    [EndpointDescription("Logs out the current user by clearing the authentication cookie.")]
    public static async Task<Results<Ok, UnauthorizedHttpResult>> Logout(
        SignInManager<ApplicationUser> signInManager,
        ISender sender,
        [FromBody] object empty
    )
    {
        if (empty != null)
        {
            await sender.Send(new LogActivityCommand("auth.logout", "Cierre de sesión"));
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
        return TypedResults.Ok<IReadOnlyList<RoleInfoDto>>([
            new(Roles.Administrator, "Administrador", "Gestión total, API keys y usuarios."),
            new(Roles.Analyst, "Analista", "Búsquedas, reportes y monitoreos periódicos."),
            new(Roles.Consulta, "Consulta", "Solo lectura del tablero y exportaciones."),
        ]);
    }

    [EndpointSummary("Listar usuarios y roles")]
    public static async Task<Ok<IReadOnlyList<AdminUserDto>>> ListUsers(
        UserManager<ApplicationUser> userManager
    )
    {
        var users = userManager.Users.OrderBy(u => u.UserName).ToList();
        var result = new List<AdminUserDto>(users.Count);
        foreach (var user in users)
        {
            result.Add(await ToDtoAsync(userManager, user));
        }

        return TypedResults.Ok<IReadOnlyList<AdminUserDto>>(result);
    }

    [EndpointSummary("Crear usuario con rol")]
    public static async Task<Results<Created<AdminUserDto>, ValidationProblem>> CreateUser(
        UserManager<ApplicationUser> userManager,
        ISender sender,
        CreateUserRequest request
    )
    {
        var email = request.Email?.Trim() ?? string.Empty;
        if (!AssignableRoles.Contains(request.Role))
        {
            return InvalidRole();
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]> { ["email"] = ["Correo electrónico no válido."] }
            );
        }

        var user = new ApplicationUser { UserName = email, Email = email };
        var created = await userManager.CreateAsync(user, request.Password ?? string.Empty);
        if (!created.Succeeded)
        {
            return IdentityProblem(created);
        }

        await userManager.AddToRoleAsync(user, request.Role);
        await sender.Send(
            new LogActivityCommand("user.created", $"Usuario {email} creado con rol {request.Role}")
        );

        var dto = await ToDtoAsync(userManager, user);
        return TypedResults.Created($"/api/Users/admin/users/{user.Id}", dto);
    }

    [EndpointSummary("Cambiar rol de un usuario")]
    public static async Task<Results<Ok<AdminUserDto>, NotFound, ValidationProblem>> ChangeUserRole(
        UserManager<ApplicationUser> userManager,
        ClaimsPrincipal principal,
        ISender sender,
        string id,
        ChangeUserRoleRequest request
    )
    {
        if (!AssignableRoles.Contains(request.Role))
        {
            return InvalidRole();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        if (
            currentRoles.Contains(Roles.Administrator)
            && request.Role != Roles.Administrator
            && await IsLastAdministratorAsync(userManager, user)
        )
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["role"] = ["Debe existir al menos un Administrador."],
                }
            );
        }

        await userManager.RemoveFromRolesAsync(user, currentRoles);
        await userManager.AddToRoleAsync(user, request.Role);
        await userManager.UpdateSecurityStampAsync(user);
        await sender.Send(
            new LogActivityCommand(
                "user.role_changed",
                $"{user.UserName}: {string.Join(",", currentRoles)} → {request.Role} (por {principal.Identity?.Name})"
            )
        );

        return TypedResults.Ok(await ToDtoAsync(userManager, user));
    }

    [EndpointSummary("Desbloquear un usuario")]
    public static async Task<Results<Ok<AdminUserDto>, NotFound>> UnlockUser(
        UserManager<ApplicationUser> userManager,
        ISender sender,
        string id
    )
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        await sender.Send(new LogActivityCommand("user.unlocked", $"{user.UserName} desbloqueado"));
        return TypedResults.Ok(await ToDtoAsync(userManager, user));
    }

    [EndpointSummary("Eliminar un usuario")]
    public static async Task<Results<NoContent, NotFound, ValidationProblem>> DeleteUser(
        UserManager<ApplicationUser> userManager,
        ClaimsPrincipal principal,
        ISender sender,
        string id
    )
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        if (userManager.GetUserId(principal) == user.Id)
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["id"] = ["No puede eliminar su propia cuenta."],
                }
            );
        }

        if (await IsLastAdministratorAsync(userManager, user))
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["id"] = ["Debe existir al menos un Administrador."],
                }
            );
        }

        var userName = user.UserName;
        await userManager.DeleteAsync(user);
        await sender.Send(new LogActivityCommand("user.deleted", $"Usuario {userName} eliminado"));
        return TypedResults.NoContent();
    }

    private static async Task<bool> IsLastAdministratorAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user
    )
    {
        if (!await userManager.IsInRoleAsync(user, Roles.Administrator))
        {
            return false;
        }

        var administrators = await userManager.GetUsersInRoleAsync(Roles.Administrator);
        return administrators.Count <= 1;
    }

    private static async Task<AdminUserDto> ToDtoAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user
    )
    {
        var roles = await userManager.GetRolesAsync(user);
        var lockoutEnd = await userManager.GetLockoutEndDateAsync(user);
        return new AdminUserDto(
            user.Id,
            user.UserName ?? user.Email ?? user.Id,
            user.Email,
            roles.ToArray(),
            lockoutEnd is not null && lockoutEnd > DateTimeOffset.UtcNow,
            user.AccessFailedCount
        );
    }

    private static ValidationProblem InvalidRole() =>
        TypedResults.ValidationProblem(
            new Dictionary<string, string[]>
            {
                ["role"] = [$"Rol no válido. Use: {string.Join(", ", AssignableRoles)}."],
            }
        );

    private static ValidationProblem IdentityProblem(IdentityResult result) =>
        TypedResults.ValidationProblem(
            result
                .Errors.GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray())
        );
}

public record UserProfileDto(
    string Id,
    string UserName,
    string? Email,
    IReadOnlyList<string> Roles
);

public record RoleInfoDto(string Name, string DisplayName, string Description);

public record AdminUserDto(
    string Id,
    string UserName,
    string? Email,
    IReadOnlyList<string> Roles,
    bool IsLockedOut,
    int AccessFailedCount
);

public record CreateUserRequest(string Email, string Password, string Role);

public record ChangeUserRoleRequest(string Role);
