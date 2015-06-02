using System;
using System.Configuration;
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

					var buildTypeId = string.Format("{0}_{1}", pushData.Repository.Name.ToLower(), pushData.Branch.ToLower()).Replace("-", "");
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

		private static string translateUser(UserData user)
		{
			if (user == null || string.IsNullOrEmpty(user.Email))
				return "Ryan";

			switch (user.Email.ToLower())
			{
				//Applications Development Developers
				case "gcox18@gmail.com":
				case "george@tundaware.com":
				case "george.cox@avispl.com":
					return "George";
				case "michael.taylor@avispl.com":
					return "mtaylor";
				case "ken.pomella@avispl.com":
					return "kpomella";
				//Internet Marketing Developers
				case "ken.cabrera@avispl.com":
					return "kcabrera";
				case "shawn.dreier@avispl.com":
					return "sdreier";
				// Gotta have a default
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