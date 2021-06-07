using System;
using System.Collections.Generic;
using System.Linq;

namespace DecisionsWorker
{
	internal class Program : DemonBase
	{
		private const string TempDir = "files";

		private static readonly Dictionary<string, byte[]> FileHeaders = new()
		{
			{"doc", new byte[] {0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1}},
			{"docx", new byte[] {0x50, 0x4B, 0x03, 0x04}},
			{"pdf", new byte[] {0x25, 0x50, 0x44, 0x46}},
			{"rtf", new byte[] {0x7B, 0x5C, 0x72, 0x74, 0x66, 0x31}}
		};

		private StateWorker stateWorker;
		private DataDownloader downloader;
		private FirstSpecWorker firstSpecWorker;
		private SecondSpecWorker<SudrfDecision> secondSpecSaver;

		protected override void DemonMain(DemonSettings settings, string[] args)
		{
			stateWorker = new StateWorker(settings, args);
			downloader = new DataDownloader(settings.Get("site"), TempDir, preLoadTimeout:settings.Get<int>("preLoadTimeout"));
			firstSpecWorker = new FirstSpecWorker(settings);
			secondSpecSaver = new SecondSpecWorker<SudrfDecision>(settings.SecondSpecSets["secondSpecSaver"]);

			if (stateWorker.SkipUpdate)
				return;

			var oldOffset = stateWorker.Offset;
			do
			{
				new SecondSpecWorker<SudrfMsk>(settings.SecondSpecSets["secondSpecLoader"])
					.InTimeAction(DownloadRecords, record => record.Case.DecisionUrl.IsSignificant(), stateWorker.Offset);
			} while (oldOffset != stateWorker.Offset);

			stateWorker.FullSave();
			Log.Info("Demon in coop");
		}

		private void DownloadRecords(List<SudrfMsk> sourceRecords, long newOffset)
		{
			var oldOffset = stateWorker.Offset;
			if (oldOffset == newOffset)
				return;

			var errorRecords = 0;
			var skippedRecords = 0;
			var newRecords = 0;
			var saveRecords = sourceRecords
				.Select(sudrfMsk =>
				{
					var caseInfo = sudrfMsk.Case;
					var url = caseInfo.DecisionUrl;
					if (!url.IsSignificant())
						return null;

					if (stateWorker.HasRecord(url))
					{
						skippedRecords++;
						return null;
					}

					if (!downloader.TryGetFile(url, out var fileData))
					{
						errorRecords++;
						Log.Error($"Can't download file and skip '{url}'");
						return null;
					}

					var extension = GetExtension(fileData);
					if (extension == null)
						throw new Exception($"Can't verify file extension from '{url}'");

					var decision = new SudrfDecision(caseInfo.Id.Id, stateWorker.DemonDt)
					{
						//set up some fields
					};
					var firstSpecName = decision.FirstSpecId + "." + decision.FileExtension;
					decision.FirstSpecLink = firstSpecWorker.Prefix + firstSpecName;
					firstSpecWorker.Save(firstSpecName, fileData);

					stateWorker.AddRecord(url);
					newRecords++;
					return decision;
				})
				.WhereNotNull();

			secondSpecSaver.Save(saveRecords);

			stateWorker.Offset = newOffset;
			stateWorker.Save();
			WriteDelta($"Downloaded data from {oldOffset} to {newOffset}", newRecords, skippedRecords, errorRecords);
		}

		private static void WriteDelta(string message, int newRecords = 0, int skippedRecords = 0, int errorRecords = 0)
		{
			Log.DemonDelta(new Dictionary<string, long>
			{
				["newRecords"] = newRecords,
				["skippedRecords"] = skippedRecords,
				["errorRecords"] = errorRecords
			});
			Log.Info(message);
		}

		private static string GetExtension(byte[] data)
		{
			foreach (var kvp in FileHeaders)
			{
				if (data.Length < kvp.Value.Length)
					continue;
				
				var fileBytes = new byte[kvp.Value.Length];
				Buffer.BlockCopy(data, 0, fileBytes, 0, kvp.Value.Length);
				if (fileBytes.SequenceEqual(kvp.Value))
					return kvp.Key;
			}
			return null;
		}
	}
}