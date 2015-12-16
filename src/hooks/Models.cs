namespace GithubTools.Hooks.Models
{
	public class PullRequestResponseData
	{
		public string Action { get; set; }
		public int Number { get; set; }
		public PullRequestData PullRequest { get; set; }
		public SenderData Sender { get; set; }
	}

	public class PullRequestData
	{
		public BaseData Base { get; set; }
	}

	public class BaseData
	{
		public string Ref { get; set; }
		public RepoData Repo { get; set; }
	}

	public class SenderData
	{
		public string Login { get; set; }
	}

	public class PushData
	{
		private string _ref;
		public string Ref
		{
			get { return _ref; }
			set
			{
				_ref = value;
				Branch = _ref.Replace("refs/heads/", "");
			}
		}
		public string Branch { get; set; }
		public RepoData Repository { get; set; }
		public SenderData Sender { get; set; }
	}

	public class RepoData
	{
		public string Name { get; set; }
		public string Url { get; set; }
	}
}