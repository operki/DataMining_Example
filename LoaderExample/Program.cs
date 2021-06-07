using System;
using System.Collections.Generic;
using System.Linq;

namespace LoaderExample
{
    internal class Program : DemonBase
	{
		private const int FirstYear = 2015;
		private DataDownloader downloader;
		private PageParser parser;
		
		protected override void DemonMain(DemonSettings settings, string[] args)
		{
			var stateWorker = new RecordsWorker(settings, args);
			var firstSpecWorker = new FirstSpecWorker(settings);
			downloader = new DataDownloader(settings.Get("site"), preLoadTimeout:settings.Get<int>("preLoadTimeout"));
			parser = new PageParser(settings["parser"]);

			if (stateWorker.SkipUpdate)
				return;

			if (!downloader.TryGet(null, out var metaPage))
			{
				Log.Fatal($"Can't get meta data from '{settings.Get("site")}'");
				return;
			}

			var lastYear = stateWorker.DemonDt.Year;
			var startYear = !stateWorker.IsInitialized || stateWorker.DemonDt.Day == 21
				? FirstYear
				: lastYear - settings.Get<int>("yearsCount");

			var courtMetas = UrlHelper.ParseMetaPage(metaPage);
			Enumerable.Range(startYear, lastYear - startYear + 1)
				.SelectMany(year => year != lastYear
					? GetMonthDates(year, 12)
					: GetMonthDates(year, stateWorker.DemonDt.Month))
				.Reverse()
				.ForEach(date =>
				{
					GetSearchBuilds(courtMetas, date)
						.Select(searchBuild => new SudrfMskFirstSpecData(searchBuild, stateWorker.DemonDt, DownloadSearchUrls(searchBuild)
							.SelectMany()
							.Select(DownloadTab)
							.WhereNotDefault()))
						.Where(firstSpecData => firstSpecData.Tabs.IsSignificant() && stateWorker.TryAddRecord(firstSpecData.Id, HashHelper.GetHash(firstSpecData)))
						.ForEach(firstSpecData => firstSpecWorker.SaveJson(firstSpecData.FirstSpecName, firstSpecData));
					
					stateWorker.Save();
					Log.DemonDelta(stateWorker.GetDelta()
						.ToDictSafe(kvp => kvp.Key + date.Year, kvp => kvp.Value));
				});

			stateWorker.FullSave();
			Log.Info("Demon in coop");
		}

		private static IEnumerable<DateTime> GetMonthDates(int year, int lastMonth)
			=> Enumerable.Range(1, lastMonth).Select(month => new DateTime(year, month, 1));
		
		private static IEnumerable<SearchBuild> GetSearchBuilds(IEnumerable<CourtMeta> courtMetas, DateTime startDt)
		{
			var instances = EnumHelper.GetAllValues<SudrfMskInstance>();
			var processTypes = EnumHelper.GetAllValues<SudrfMskProcessType>();

			return courtMetas
				.SelectMany(court => instances.Select(instance => (court, instance))
					.SelectMany(tuple => processTypes.Select(processType => (tuple.court, tuple.instance, processType))))
						.Select(triple => new SearchBuild(triple.court, triple.instance, triple.processType, startDt));
		}

		private IEnumerable<List<string>> DownloadSearchUrls(SearchBuild searchBuild)
		{
			var currentPage = 1;
			while (true)
			{
				var searchUrl = UrlHelper.GetSearchUrl(searchBuild, currentPage);
				if (!downloader.TryGet(searchUrl, out var pageData))
				{
					Log.Fatal($"Can't download page '{searchUrl}'. Skip this {currentPage} page number");
					currentPage++;
					continue;
				}
				
				if (!parser.TryGetTableUrls(pageData, out var urls))
					yield break;

				yield return urls;
				currentPage++;
			}
		}

		private (string url, string page) DownloadTab(string searchUrl)
		{
			if (downloader.TryGet(searchUrl, out var pageData))
				return (searchUrl, parser.GetTab(pageData));
			
			Log.Fatal($"Can't download page '{searchUrl}'. Skip this tab");
			return default;
		}
	}
}