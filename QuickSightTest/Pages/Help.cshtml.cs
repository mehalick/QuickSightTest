using Amazon.QuickSight;
using Amazon.QuickSight.Model;
using Amazon.SecurityToken.Model;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace QuickSightTest.Pages;

public class HelpModel : PageModel
{
	private static readonly Amazon.RegionEndpoint Region = Amazon.RegionEndpoint.USEast1;
	private static readonly string AwsAccountId = "965753389244";
	private static readonly string QsNamespace = "default";
	private const int AssumeRoleDuration = 1600;
	private static readonly string IamRole = "arn:aws:iam::965753389244:role/QuickSightEmbedRole";

	public string EmbedUrl { get; set; } = default!;

	public async Task OnGetAsync()
	{
		var userName = HttpContext.User.Claims.FirstOrDefault(c => c.Type == "cognito:username")?.Value ?? throw new Exception("No username claim");

		var credentials = await AssumeRole(userName, IamRole);
		var quickSightClient = new AmazonQuickSightClient(credentials, Region);

		var qsUserName = "QuickSightEmbedRole/" + userName;
		var list = await ListQuickSightUsers(quickSightClient);

		User user;

		if (list.Any(l => l.UserName == qsUserName))
		{
			user = list.First(l => l.UserName == qsUserName);
		}
		else
		{
			/* Step 3: Provision the QuickSight user if the user does not exist */
			user = await RegisterQuickSightUser(quickSightClient, userName, IamRole);
		}

		EmbedUrl = await RetrieveQuickSightEmbedUrl(quickSightClient, user!.Arn);
	}

	public async Task<Credentials> AssumeRole(string userName, string iamRole)
	{
		var assumeRoleReq = new AssumeRoleRequest
		{
			DurationSeconds = AssumeRoleDuration, //the duration, in seconds, of the role session
			RoleSessionName = userName, //unique identifier for the assumed role session. In this example, qsbloguser@example.com
			RoleArn = iamRole //ARN of the IAM role to assume
		};

		// Application identity is attached to this client from your application configuration.
		var tokenServiceClient = new Amazon.SecurityToken.AmazonSecurityTokenServiceClient(Region);

		var response = await tokenServiceClient.AssumeRoleAsync(assumeRoleReq);

		return response.Credentials;
	}

	/*Step 2: Retrieve list of users*/

	public async Task<List<User>> ListQuickSightUsers(AmazonQuickSightClient client)
	{
		var listUsersRequest = new ListUsersRequest
		{
			Namespace = QsNamespace,
			AwsAccountId = AwsAccountId
		};

		var response = await client.ListUsersAsync(listUsersRequest);

		return response.UserList;
	}

	/* Step 3: Provision the QuickSight user */

	public async Task<User> RegisterQuickSightUser(AmazonQuickSightClient client, string userName, string iamRole)
	{
		var response = await client.RegisterUserAsync(new RegisterUserRequest
		{
			Email = userName,
			IdentityType = IdentityType.IAM,
			UserRole = UserRole.READER,
			IamArn = iamRole, //ARN of the IAM role - QuickSightEmbedRole
			SessionName = userName,//qsbloguser@example.com
			AwsAccountId = AwsAccountId,
			Namespace = QsNamespace
		});

		return response.User;
	}

	/*Step 4: Retrieve the QuickSight embed url*/

	public async Task<string> RetrieveQuickSightEmbedUrl(AmazonQuickSightClient client, string userArn)
	{
		var registeredUserEmbeddingExperienceConfiguration = GetDashboardEmbeddingConfiguration("e35aa24c-7382-499f-bbb9-3e3f750cdc98");

		var allowedDomains = new[] { "https://localhost:64449", "https://localhost" }; // pass the application domains here

		var response = await client.GenerateEmbedUrlForRegisteredUserAsync(new GenerateEmbedUrlForRegisteredUserRequest
		{
			AwsAccountId = AwsAccountId,
			ExperienceConfiguration = registeredUserEmbeddingExperienceConfiguration,
			UserArn = userArn,

			//AllowedDomains = allowedDomains.ToList(),
			//SessionLifetimeInMinutes = 100, //how many minutes the session is valid
		});

		return response.EmbedUrl;
	}

	//Dashboard embedding experience
	public RegisteredUserEmbeddingExperienceConfiguration GetDashboardEmbeddingConfiguration(string dashboardId)
	{
		return new RegisteredUserEmbeddingExperienceConfiguration
		{
			Dashboard = new RegisteredUserDashboardEmbeddingConfiguration
			{
				InitialDashboardId = dashboardId
			}
		};
	}

	//Fine grained visual embedding experience
	public RegisteredUserEmbeddingExperienceConfiguration GetVisualEmbeddingConfiguration(string dashboardId, string sheetId, string visualId)
	{
		var dashboardVisualId = new DashboardVisualId
		{
			// The DashboardId, SheetId, and VisualId can be found in the IDs for developers section of the Embed visual pane of the visual's on-visual menu of the Amazon QuickSight console.
			DashboardId = dashboardId,
			SheetId = sheetId,
			VisualId = visualId
		};
		var config = new RegisteredUserDashboardVisualEmbeddingConfiguration
		{
			InitialDashboardVisualId = dashboardVisualId
		};

		return new RegisteredUserEmbeddingExperienceConfiguration
		{
			DashboardVisual = config
		};
	}
}
