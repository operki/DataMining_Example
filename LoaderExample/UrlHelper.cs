using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace LoaderExample
{
	public static class UrlHelper
	{
		private const string MetaStartText = "courts:";
		private const string MetaEndText = "]";
		
		private const string MethodName = "search";
		private const string FormType = "fullForm";
		private static readonly List<string> AllSearchParams = new()
		{
			"formType", "courtAlias", "uid",
			"instance", "processType", "category",
			"letterNumber", "caseNumber", "participant",
			"codex", "judge", "publishingState",
			"documentType", "documentText", "year",
			"caseDateFrom", "caseDateTo", "caseFinalDateFrom",
			"caseFinalDateTo", "caseLegalForceDateFrom", "caseLegalForceDateTo",
			"docsDateFrom", "docsDateTo", "documentStatus",
			"page"
		};
		
		public static List<CourtMeta> ParseMetaPage(string pageData)
		{
			var pageStart = pageData.Substring(pageData.IndexOf(MetaStartText, StringComparison.Ordinal) + MetaStartText.Length);
			var pageJson = pageStart.Substring(0, pageStart.IndexOf(MetaEndText, StringComparison.Ordinal) + MetaEndText.Length);
			var serializer1 = new JsonSerializer();
			var stringReader = new StringReader(pageJson);
			using var reader = new JsonTextReader(stringReader);
			return serializer1.Deserialize<List<CourtMeta>>(reader);
		}

		public static string GetSearchUrl(SearchBuild searchBuild, int pageNumber)
		{
			var searchDict = new Dictionary<string, string>
			{
				["formType"] = FormType,
				["courtAlias"] = searchBuild.CourtMeta.Alias,
				["instance"] = ((int)searchBuild.Instance).ToString(),
				["processType"] = ((int)searchBuild.ProcessType).ToString(),
				["caseDateFrom"] = searchBuild.StartDt.ToDigitDateString(),
				["caseDateTo"] = searchBuild.EndDt.ToDigitDateString(),
				["page"] = pageNumber.ToString()
			};
			return searchBuild.CourtMeta.AbsolutePath
			       + "/" + MethodName
			       + "?" + string.Join("&", AllSearchParams
				       .Select(param => searchDict.ContainsKey(param)
					       ? param + "=" + searchDict[param]
					       : param + "="));
		}
	}
}