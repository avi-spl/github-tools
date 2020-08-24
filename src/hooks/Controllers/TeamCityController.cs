using GitHubTools.Hooks.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace GitHubTools.Hooks.Controllers
{
	[Route("teamcity")]
	[ApiController]
	public class TeamCityController : ControllerBase
	{
		private readonly JsonSerializerSettings _snakeCaseJsonSerializerSettings = new JsonSerializerSettings() { ContractResolver = new SnakeCaseResolver() };
		private readonly IOptions<TeamCityConfigData> _teamCityConfigData;

		public TeamCityController(IOptions<TeamCityConfigData> teamCityConfigData)
		{
			_teamCityConfigData = teamCityConfigData;
		}

		[HttpPost]
		public ActionResult<string> Post()
		{
			EventType eventType = DetermineEventType(Request.Headers);

			switch (eventType)
			{
				case EventType.Push:
					HandlePushEvent(JsonConvert.DeserializeObject<PushData>(Request.Form["payload"]));
					break;
				case EventType.PullRequest:
					HandlePullRequestEvent(JsonConvert.DeserializeObject<PullRequestResponseData>(Request.Form["payload"], _snakeCaseJsonSerializerSettings));
					break;
			}

			return Ok();
		}

		private enum EventType
		{
			Unknown,
			Push,
			PullRequest
		}

		private static EventType DetermineEventType(IHeaderDictionary headers)
		{
			var result = EventType.Unknown;

			if (headers.ContainsKey("X-GitHub-Event"))
			{
				var eventHeader = headers["X-GitHub-Event"].First().ToUpper();
				switch (eventHeader)
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

		private void HandlePushEvent(PushData data)
		{
			var buildTypeId = string.Format("{0}_{1}", data.Repository.Name.ToLower(), data.Branch.ToLower()).Replace("-", "");
			var githubLogin = data.Sender.Login;
			var teamCityUserName = LookupTeamCityUserName(githubLogin);
			TriggerTeamCityBuild(teamCityUserName, buildTypeId);
		}

		private void HandlePullRequestEvent(PullRequestResponseData data)
		{
			if (data.Action == "opened" || data.Action == "synchronize" || data.Action == "ready_for_review") // new PR, or commits were pushed to existing PR, or PR switching from Draft to "ready"
			{
				var pullRequest = data.PullRequest;

				// is this a draft PR? (assume it is not if we couldn't parse the "Draft" property)
				if (pullRequest.Draft ?? false)
				{
					// yep; no trigger for you!
					return;
				}

				var branchName = data.Number.ToString();
				var buildTypeId = string.Format("{0}_{1}", pullRequest.Base.Repo.Name.ToLower(), pullRequest.Base.Ref.ToLower()).Replace("-", "");

				var githubLogin = data.Sender.Login;
				var teamCityUserName = LookupTeamCityUserName(githubLogin);

				TriggerTeamCityBuild(teamCityUserName, buildTypeId, branchName);
			}
		}

		private string LookupTeamCityUserName(string githubLogin)
		{
			var githubToTeamCity = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["gcox"] = "George",
				["kevinrood"] = "krood",
				["mondavired"] = "mtaylor",
				["ixtlan07"] = "rlane",
				["jlederman"] = "jlederman"
			};

			const string defaultTeamCityUserName = "Ryan";

			return githubToTeamCity.ContainsKey(githubLogin) ? githubToTeamCity[githubLogin] : defaultTeamCityUserName;
		}

		private void TriggerTeamCityBuild(string teamCityUserName, string buildTypeId, string branchName = null)
		{
			var url = string.Format(_teamCityConfigData.Value.BuildTriggerUrl, buildTypeId);
			var credentials = new NetworkCredential(teamCityUserName, teamCityUserName.ToLower());

			var payload = "<build";
			if (!string.IsNullOrEmpty(branchName))
				payload += " branchName=\"" + branchName + "\"";
			payload += ">";
			payload += "<buildType id=\"" + buildTypeId + "\"/>";
			payload += "</build>";
			var payloadRaw = Encoding.UTF8.GetBytes(payload);

			var request = WebRequest.Create(url);
			request.ContentType = "application/xml";
			request.ContentLength = payloadRaw.Length;
			request.Method = "POST";
			request.Credentials = credentials;

			var stream = request.GetRequestStream();
			stream.Write(payloadRaw, 0, payloadRaw.Length);
			stream.Close();

			request.GetResponse();
		}
	}
}
