using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PubQuizCreator.Web.Pages
{
    public class LoginModel(IConfiguration configuration)
        : PageModel
    {
        #region Public Properties

        public string? Error { get; private set; }

        #endregion Public Properties

        #region Public Methods

        public IActionResult OnGet()
        {
            // Already logged in — redirect to app
            if (User.Identity?.IsAuthenticated == true)
            {
                return Redirect("/");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string username, string password, string? returnUrl)
        {
            var expectedUser = configuration["Auth:Username"];
            var expectedPass = configuration["Auth:Password"];

            if (string.IsNullOrWhiteSpace(expectedUser) || string.IsNullOrWhiteSpace(expectedPass))
            {
                Error = "Auth credentials not configured on server.";
                return Page();
            }

            if (username != expectedUser || password != expectedPass)
            {
                Error = "Invalid username or password.";
                return Page();
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, username),
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true });

            return Redirect(returnUrl ?? "/");
        }

        #endregion Public Methods
    }
}