using AnalisisSentimiento.Domain.Entities;
using AnalisisSentimiento.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AnalisisSentimiento.Infrastructure.Identity;

public class AuditingSignInManager(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<ApplicationUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation,
    ApplicationDbContext context
)
    : SignInManager<ApplicationUser>(
        userManager,
        contextAccessor,
        claimsFactory,
        optionsAccessor,
        logger,
        schemes,
        confirmation
    )
{
    public override async Task<SignInResult> PasswordSignInAsync(
        string userName,
        string password,
        bool isPersistent,
        bool lockoutOnFailure
    )
    {
        var result = await base.PasswordSignInAsync(
            userName,
            password,
            isPersistent,
            lockoutOnFailure
        );

        var user = await UserManager.FindByNameAsync(userName);
        var action =
            result.Succeeded ? "auth.login"
            : result.IsLockedOut ? "auth.locked"
            : "auth.failed";
        var detail =
            result.Succeeded ? $"Inicio de sesión de {userName}"
            : result.IsLockedOut
                ? $"Cuenta {userName} bloqueada temporalmente por intentos fallidos"
            : $"Intento fallido de inicio de sesión para {userName}";

        context.ActivityLogEntries.Add(ActivityLogEntry.Create(action, user?.Id, detail));
        await context.SaveChangesAsync();

        return result;
    }
}
