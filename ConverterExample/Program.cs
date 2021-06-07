using System.Collections.Generic;
using System.IO;
using System.Linq;
using Utils;

namespace ConverterExample
{
	internal class Program : DemonBase
	{
		protected override void DemonMain(DemonSettings settings, string[] args)
		{
			var stateWorker = new RecordsWorker(settings, args);
			var firstSpecWorker = new FirstSpecWorker(settings);
			var secondSpecWorker = new SecondSpecWorker<SudrfMsk>(settings);

			if (!firstSpecWorker.TryGetNewFileGroups(file => !stateWorker.HasSource(file), out var fileGroups))
			{
				stateWorker.FullSave();
				WriteDelta();
				return;
			}

			var errors = 0;
			var tabParser = new MskParser(settings["patterns"], 
				GetCourtMapper(settings.Get("courtIdsUrl"), settings.Get("courtIdsFile")), settings.Get("site"));
			fileGroups.ForEach(fileGroup =>
			{
				var newRecords = fileGroup
					.Select(file =>
					{
						stateWorker.AddSource(file);
						var firstSpecData = firstSpecWorker.LoadOrDefault<SudrfMskFirstSpecData>(file);
						if (!firstSpecData.Tabs.IsSignificant())
							return null;

						return firstSpecData.Tabs
							.Select(tuple =>
							{
								var (url, page) = tuple;
								var record = tabParser.ConvertTabPage(firstSpecData, url, page);
								if (record != null)
									return record;

								Log.Error($"Can't convert tab from '{url}'. Skip this tab. Check data in firstSpec: '{firstSpecData.FirstSpecName}'");
								errors++;
								return null;
							});
					})
					.WhereNotNull()
					.SelectMany()
					.WhereNotNull()
					.Where(record => stateWorker.TryAddRecord(record.Id.Id, HashHelper.GetHash(record)));

				secondSpecWorker.Save(newRecords);
				stateWorker.Save();
			});
			
			stateWorker.FullSave();
			WriteDelta("Demon in coop", errors, stateWorker.GetDelta());
		}

		private static void WriteDelta(string message = null, int errors = 0, Dictionary<string, long> stateDelta = null)
		{
			if (stateDelta != null)
				Log.DemonDelta(stateDelta);
			Log.DemonDelta("errors", errors);
			
			if (message != null)
				Log.Info(message);
		}

		private static Dictionary<string, string> GetCourtMapper(string courtIdsUrl, string courtIdsFile)
		{
			if (DataDownloader.JustGet(courtIdsUrl, out var courtMapperHtml, 1))
				File.WriteAllText(courtIdsFile, courtMapperHtml);
			else
				courtMapperHtml = File.ReadAllText(courtIdsFile);

			return HtmlExtensions.InitCleanDocument(courtMapperHtml)
				.GetNodes("//option")
				.Where(node => node.Attributes["value"] != null && node.InnerText.IsSignificant())
				.ToDictSafe(node => node.InnerTextTrim(), node => node.Attributes["value"].Value);
		}
	}
}