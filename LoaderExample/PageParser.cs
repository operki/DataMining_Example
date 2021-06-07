using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Utils;

namespace LoaderExample
{
	public class PageParser
	{
		private readonly string tablePattern;
		private readonly string searchUrlSubstring;
		private readonly string emptyPattern;
		private readonly string emptyText;
		private readonly string tabPattern;

		public PageParser(IReadOnlyDictionary<string, string> settings)
		{
			tablePattern = settings["tablePattern"];
			searchUrlSubstring = settings["searchUrlSubstring"];
			emptyPattern = settings["emptyPattern"];
			emptyText = settings["emptyText"];
			tabPattern = settings["tabPattern"];
		}
		
		public bool TryGetTableUrls(string pageData, out List<string> urls)
		{
			urls = null;
			var htmlDocument = HtmlExtensions.InitCleanDocument(pageData);
			var table = htmlDocument.GetNode(tablePattern);
			if (table == null)
			{
				if (htmlDocument.GetNode(emptyPattern)?.InnerTextTrim().SignificantEquals(emptyText) != true)
					return false;
				
				File.WriteAllText("errorPage.html", pageData);
				throw new Exception($"Can't find table with search results using '{tablePattern}' " +
				                    $"or text-replacement for empty results using '{emptyPattern}'. Check document structure in file 'errorPage.html'");
			}

			if (!table.TryGetLinks(out var tableUrls))
			{
				File.WriteAllText("errorPage.html", pageData);
				throw new Exception($"Can't get any urls from search results using '{tablePattern}'. Check document structure in file 'errorPage.html'");
			}

			urls = tableUrls
				.Where(url => url.Contains(searchUrlSubstring))
				.ToList();
			return true;
		}


		public string GetTab(string pageData)
		{
			if (HtmlExtensions.InitCleanDocument(pageData).TryGetNode(tabPattern, out var sectionNode))
				return sectionNode.InnerHtmlTrim();

			File.WriteAllText("errorPage.html", pageData);
			throw new Exception($"Can't find tab text on page using '{tabPattern}'. Check document structure in file 'errorPage.html'");
		}
	}
}