using System.Text.RegularExpressions;
using Newtonsoft.Json.Serialization;

namespace GithubTools.Hooks
{
	public class SnakeCaseResolver : DefaultContractResolver
	{
		/// <summary>
		/// Resolves incoming snake-case property names to their pascal-case equivalents
		/// 
		/// "property_name" -> "PropertyName"
		/// </summary>
		/// <param name="propertyName"></param>
		/// <returns></returns>
		protected override string ResolvePropertyName(string propertyName)
		{
			var firstLetterLowerCase = char.ToLower(propertyName[0]) + propertyName.Substring(1); // "PropertyName" -> "propertyName"
			return Regex.Replace(firstLetterLowerCase, @"([A-Z])", "_$1").ToLower(); // "propertyName" -> "property_name"
		}
	}
}