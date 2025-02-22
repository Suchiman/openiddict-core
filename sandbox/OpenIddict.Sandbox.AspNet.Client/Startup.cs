﻿using System;
using System.Web.Mvc;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Autofac.Integration.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Owin.Security.Cookies;
using OpenIddict.Client;
using OpenIddict.Client.Owin;
using OpenIddict.Sandbox.AspNetCore.Server.Models;
using Owin;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenIddict.Sandbox.AspNet.Client
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            var container = CreateContainer();

            // Register the Autofac scope injector middleware.
            app.UseAutofacLifetimeScopeInjector(container);

            // Register the cookie middleware responsible for storing the user sessions.
            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                ExpireTimeSpan = TimeSpan.FromMinutes(50),
                SlidingExpiration = false
            });

            // Register the OpenIddict middleware.
            app.UseMiddlewareFromContainer<OpenIddictClientOwinMiddleware>();

            // Configure ASP.NET MVC 5.2 to use Autofac when activating controller instances.
            DependencyResolver.SetResolver(new AutofacDependencyResolver(container));

            // Create the database used by the OpenIddict client stack to store tokens.
            // Note: in a real world application, this step should be part of a setup script.
            using var scope = container.BeginLifetimeScope();

            var context = scope.Resolve<ApplicationDbContext>();
            context.Database.CreateIfNotExists();
        }

        private static IContainer CreateContainer()
        {
            var services = new ServiceCollection();

            services.AddOpenIddict()

                // Register the OpenIddict core components.
                .AddCore(options =>
                {
                    // Configure OpenIddict to use the Entity Framework 6.x stores and models.
                    // Note: call ReplaceDefaultEntities() to replace the default OpenIddict entities.
                    options.UseEntityFramework()
                           .UseDbContext<ApplicationDbContext>();

                    // Developers who prefer using MongoDB can remove the previous lines
                    // and configure OpenIddict to use the specified MongoDB database:
                    // options.UseMongoDb()
                    //        .UseDatabase(new MongoClient().GetDatabase("openiddict"));
                })

                // Register the OpenIddict client components.
                .AddClient(options =>
                {
                    // Enable the redirection endpoint needed to handle the callback stage.
                    //
                    // Note: to mitigate mix-up attacks, it's recommended to use a unique redirection endpoint
                    // address per provider, unless all the registered providers support returning an "iss"
                    // parameter containing their URL as part of authorization responses. For more information,
                    // see https://datatracker.ietf.org/doc/html/draft-ietf-oauth-security-topics#section-4.4.
                    options.SetRedirectionEndpointUris(
                        "/callback/login/local",
                        "/callback/login/github",
                        "/callback/login/google",
                        "/callback/login/twitter");

                    // Enable the post-logout redirection endpoints needed to handle the callback stage.
                    options.SetPostLogoutRedirectionEndpointUris(
                        "/callback/logout/local");

                    // Register the signing and encryption credentials used to protect
                    // sensitive data like the state tokens produced by OpenIddict.
                    options.AddDevelopmentEncryptionCertificate()
                           .AddDevelopmentSigningCertificate();

                    // Register the OWIN host and configure the OWIN-specific options.
                    options.UseOwin()
                           .EnableRedirectionEndpointPassthrough()
                           .EnablePostLogoutRedirectionEndpointPassthrough();

                    // Register the System.Net.Http integration.
                    options.UseSystemNetHttp();

                    // Add a client registration matching the client application definition in the server project.
                    options.AddRegistration(new OpenIddictClientRegistration
                    {
                        Issuer = new Uri("https://localhost:44349/", UriKind.Absolute),

                        ClientId = "mvc",
                        ClientSecret = "901564A5-E7FE-42CB-B10D-61EF6A8F3654",
                        Scopes = { Scopes.Email, Scopes.Profile, Scopes.OfflineAccess, "demo_api" },

                        RedirectUri = new Uri("https://localhost:44378/callback/login/local", UriKind.Absolute),
                        PostLogoutRedirectUri = new Uri("https://localhost:44378/callback/logout/local", UriKind.Absolute)
                    });

                    // Register the Web providers integrations.
                    options.UseWebProviders()
                           .AddGitHub(new()
                           {
                               ClientId = "c4ade52327b01ddacff3",
                               ClientSecret = "da6bed851b75e317bf6b2cb67013679d9467c122",
                               RedirectUri = new Uri("https://localhost:44378/callback/login/github", UriKind.Absolute)
                           })
                           .AddGoogle(new()
                           {
                               ClientId = "1016114395689-kgtgq2p6dj27d7v6e2kjkoj54dgrrckh.apps.googleusercontent.com",
                               ClientSecret = "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",
                               RedirectUri = new Uri("https://localhost:44378/callback/login/google", UriKind.Absolute),
                               Scopes = { Scopes.Profile }
                           })
                           .AddTwitter(new()
                           {
                               ClientId = "bXgwc0U3N3A3YWNuaWVsdlRmRWE6MTpjaQ",
                               ClientSecret = "VcohOgBp-6yQCurngo4GAyKeZh0D6SUCCSjJgEo1uRzJarjIUS",
                               RedirectUri = new Uri("https://localhost:44378/callback/login/twitter", UriKind.Absolute)
                           });
                });

            // Create a new Autofac container and import the OpenIddict services.
            var builder = new ContainerBuilder();
            builder.Populate(services);

            // Register the MVC controllers.
            builder.RegisterControllers(typeof(Startup).Assembly);

            return builder.Build();
        }
    }
}
