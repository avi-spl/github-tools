using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using AVISPL.Exceptions;
using Nancy;
using Newtonsoft.Json;

namespace GithubTools.Hooks
{
	public class TeamCityHook : NancyModule
	{
		public TeamCityHook()
		{
			Post["/teamcity"] = x =>
			{
				try
				{
					PushData pushData = JsonConvert.DeserializeObject<PushData>(Request.Form["payload"]);

					var buildTypeId = getBuildTypeId(pushData.Repository.Name, pushData.Branch);
					var user = pushData.Pusher ?? pushData.Commits.OrderBy(c => c.Timestamp).First().Author;
					var teamCityUser = translateUser(user);
					sendGetRequest(buildTypeId, teamCityUser, Request.Form["payload"]);
				}
				catch (Exception ex)
				{
					ExceptionReporter.Report(ex);
				}
				return string.Empty;
			};
		}

		private static string getBuildTypeId(string repo, string branch)
		{
			string buildTypeId;
			const string cmdText = "SELECT BUILD_TYPE_ID FROM vcs_root_instance as vcs INNER JOIN agent_sources_version as asv ON asv.VCS_ROOT_ID = vcs.ID WHERE vcs.BODY LIKE @filter";
			var connectionString = ConfigurationManager.AppSettings["TeamCityDbConnectionString"];
			using (var sqlConnection = new SqlConnection(connectionString))
			{
				sqlConnection.Open();
				using (var dbCommand = new SqlCommand(cmdText, sqlConnection))
				{
					dbCommand.CommandType = CommandType.Text;
					dbCommand.Parameters.Add(new SqlParameter("@filter", string.Format(@"%branch={0}%url=%{1}%", branch, repo)));
					buildTypeId = dbCommand.ExecuteScalar() as string;
				}
				sqlConnection.Close();
			}
			return buildTypeId;
		}

		private static string translateUser(UserData user)
		{
			if (user == null || string.IsNullOrEmpty(user.Email))
				return "Ryan";

			switch (user.Email.ToLower())
			{
				case "joe.shipley@avispl.com":
					return "Joey";
				case "gcox18@gmail.com":
				case "george.cox@avispl.com":
					return "George";
				case "jennifer.reitsky@avispl.com":
					return "Jennifer";
				default:
					return "Ryan";
			}
		}

		static void sendGetRequest(string buildTypeId, string teamCityUser, string payload)
		{
			if (string.IsNullOrEmpty(buildTypeId))
			{
				ExceptionReporter.Report(new Exception(string.Format("Build Id not found. Github payload: {0}", payload)));
				return;
			}

			var url = string.Format(ConfigurationManager.AppSettings["TeamCityBuildTriggerUrl"], buildTypeId);
			var credentials = new NetworkCredential(teamCityUser, teamCityUser.ToLower());

			var wc = new WebClient { Credentials = credentials };
			wc.DownloadString(url);
		}
	}
}