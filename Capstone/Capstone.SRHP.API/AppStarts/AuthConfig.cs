﻿using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace Capstone.HPTY.API.AppStarts
{
    public static class AuthConfig
    {
        public static void ConfigureAuthService(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(configuration["Jwt:Key"])),
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ClockSkew = TimeSpan.Zero
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var authHeader = context.Request.Headers.Authorization.ToString();
                            if (!string.IsNullOrEmpty(authHeader))
                            {
                                context.Token = authHeader.Replace("Bearer ", "").Trim();
                            }
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = context =>
                        {
                            // Add role claim if it doesn't exist in the correct format
                            var identity = context.Principal.Identity as ClaimsIdentity;
                            var roleClaim = identity?.FindFirst("role");
                            if (roleClaim != null)
                            {
                                // Add the role claim in the format that [Authorize] expects
                                if (!identity.HasClaim(ClaimTypes.Role, roleClaim.Value))
                                {
                                    identity.AddClaim(new Claim(ClaimTypes.Role, roleClaim.Value));
                                }
                            }

                            return Task.CompletedTask;
                        }
                    };
                });

            // Only add Google authentication if configuration exists
            var googleClientId = configuration["Authentication:Google:ClientId"];
            var googleClientSecret = configuration["Authentication:Google:ClientSecret"];

            if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
            {
                services.AddAuthentication()
                    .AddGoogle(options =>
                    {
                        options.ClientId = googleClientId;
                        options.ClientSecret = googleClientSecret;
                        options.CallbackPath = "/signin-google";
                    });
            }

            services.AddAuthorization(options =>
            {
                options.AddPolicy("RequireAdminRole", policy =>
                    policy.RequireRole("Admin"));
            });
        }
    }
}