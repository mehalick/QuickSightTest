using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace QuickSightTest;

public class Startup
{
	public Startup(IConfiguration configuration)
	{
		Configuration = configuration;
	}

	public IConfiguration Configuration { get; }

	// This method gets called by the runtime. Use this method to add services to the container.
	public void ConfigureServices(IServiceCollection services)
	{
		//services.AddCognitoIdentity();

		services.AddAuthentication(item =>
			{
				item.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
				item.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
			})
			.AddCookie()
			.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
			{
				options.ResponseType = Configuration["Authentication:Cognito:ResponseType"];
				options.MetadataAddress = Configuration["Authentication:Cognito:MetadataAddress"];
				options.ClientId = Configuration["Authentication:Cognito:ClientId"];
				options.ClientSecret = Configuration["Authentication:Cognito:ClientSecret"];
				options.Events = new OpenIdConnectEvents()
				{
					// This method enables logout from Amazon Cognito, and it is invoked before redirecting to the identity provider to sign out
					OnRedirectToIdentityProviderForSignOut = OnRedirectToIdentityProviderForSignOut
				};
				options.Scope.Clear();
				options.Scope.Add("openid");
				options.Scope.Add("email");
				options.Scope.Add("phone");
				options.SaveTokens = Convert.ToBoolean(Configuration["Authentication:Cognito:SaveToken"]);
			});

		Task OnRedirectToIdentityProviderForSignOut(RedirectContext context)
		{
			context.ProtocolMessage.Scope = "openid";
			context.ProtocolMessage.ResponseType = "code";

			var cognitoDomain = Configuration["Authentication:Cognito:CognitoDomain"];

			var clientId = Configuration["Authentication:Cognito:ClientId"];

			var logoutUrl = $"{context.Request.Scheme}://{context.Request.Host}{Configuration["Authentication:Cognito:AppSignOutUrl"]}";

			context.ProtocolMessage.IssuerAddress = $"{cognitoDomain}/logout?client_id={clientId}&logout_uri={logoutUrl}";

			return Task.CompletedTask;
		}

		services.AddRazorPages(options =>
		{
			options.Conventions.AuthorizePage("/Help");
		});
	}

	// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
	public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
	{
		if (env.IsDevelopment())
		{
			app.UseDeveloperExceptionPage();
		}
		else
		{
			app.UseExceptionHandler("/Error");
			// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
			app.UseHsts();
		}

		app.UseHttpsRedirection();
		app.UseStaticFiles();

		app.UseRouting();

		app.UseAuthentication();
		app.UseAuthorization();

		app.UseEndpoints(endpoints =>
		{
			endpoints.MapRazorPages();
		});
	}
}
