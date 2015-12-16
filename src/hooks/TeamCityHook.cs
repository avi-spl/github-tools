using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using AVISPL.Exceptions;
using Nancy;
using Newtonsoft.Json;

using GithubTools.Hooks.Models;

namespace GithubTools.Hooks
{
	public class TeamCityHook : NancyModule
	{
		private enum EventType
		{
			Unknown,
			Push,
			PullRequest
		}
		private readonly JsonSerializerSettings _snakeCaseJsonSerializerSettings =
			new JsonSerializerSettings()
			{
				ContractResolver = new SnakeCaseResolver()
			};

		public TeamCityHook()
		{
			Post["/teamcity"] = x =>
			{
				try
				{
					EventType eventType = DetermineEventType(Request);
					
					switch(eventType)
					{
						case EventType.Push:
							HandlePushEvent(JsonConvert.DeserializeObject<PushData>(Request.Form["payload"]));
							break;
						case EventType.PullRequest:
							HandlePullRequestEvent(JsonConvert.DeserializeObject<PullRequestResponseData>(Request.Form["payload"], _snakeCaseJsonSerializerSettings));
							break;
					}
				}
				catch (Exception ex)
				{
					ExceptionReporter.Report(ex);
				}
				return string.Empty;
			};
		}

		private static EventType DetermineEventType(Request request)
		{
			var result = EventType.Unknown;

			if (request.Headers.ContainsKey("X-GitHub-Event"))
			{
				var eventHeader = request.Headers["X-GitHub-Event"].First().ToUpper();
				switch(eventHeader)
				{
					case "PUSH":
						result = EventType.Push;
						break;
					case "PULL_REQUEST":
						result = EventType.PullRequest;
						break;
				}
			}

			return result;
		}

		private static void HandlePushEvent(PushData data)
		{
			var buildTypeId = string.Format("{0}_{1}", data.Repository.Name.ToLower(), data.Branch.ToLower()).Replace("-", "");
			var githubLogin = data.Sender.Login;
			var teamCityUserName = lookupTeamCityUserName(githubLogin);
			triggerTeamCityBuild(teamCityUserName, buildTypeId);
		}

		private static void HandlePullRequestEvent(PullRequestResponseData data)
		{
			if (data.Action == "opened" || data.Action == "synchronize") // new PR, or commits were pushed to existing PR
			{
				var pullRequest = data.PullRequest;
				var branchName = data.Number.ToString();
				var buildTypeId = string.Format("{0}_{1}", pullRequest.Base.Repo.Name.ToLower(), pullRequest.Base.Ref.ToLower()).Replace("-", "");

				var githubLogin = data.Sender.Login;
				var teamCityUserName = lookupTeamCityUserName(githubLogin);

				triggerTeamCityBuild(teamCityUserName, buildTypeId, branchName);
			}
		}

		private static string lookupTeamCityUserName(string githubLogin)
		{
			var githubToTeamCity = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["gcox"] = "George",
				["kevinrood"] = "krood",
				["mondavired"] = "mtaylor",
				["ixtlan07"] = "rlane"
			};

			var defaultTeamCityUserName = "Ryan";

			return githubToTeamCity.ContainsKey(githubLogin) ? githubToTeamCity[githubLogin] : defaultTeamCityUserName;
		}

		static void triggerTeamCityBuild(string teamCityUserName, string buildTypeId, string branchName = null)
		{
			var url = string.Format(ConfigurationManager.AppSettings["TeamCityBuildTriggerUrl"], buildTypeId);
			if (!string.IsNullOrEmpty(branchName))
			{
				url += string.Format("&branchName={0}", branchName);
			}
			var credentials = new NetworkCredential(teamCityUserName, teamCityUserName.ToLower());

			var wc = new WebClient { Credentials = credentials };
			wc.DownloadString(url);
		}
	}
}