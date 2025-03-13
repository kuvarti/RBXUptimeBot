using Google.Apis.Sheets.v4.Data;
using Google.Apis.Sheets.v4;

namespace RBXUptimeBot.Classes
{
	public partial class Account
	{
		static readonly Dictionary<string, string> Columns = new Dictionary<string, string> {
			{"Token", "E"},
			{"TokenCreatedTime", "F"},
			{"Status", "G"},
			{"State", "H"},
			//{"Proxy", "I" }, This line just claims 'G' column and we dont change it.
			{"LastUpdate", "J"}
		};

		private bool CheckINIparams()
		{
			if (!AccountManager.GSheet.Exists("SpreadsheetId")) return false;
			if (!AccountManager.GSheet.Exists("AccountsTableName")) return false;
			return true;
		}

		private async Task UpdateCell(ValueRange actualRange)
		{
			if (!CheckINIparams())
			{
				Logger.Error("Google Sheet values cannot be updated.", new Exception(".INI file params is not setted"));
				return;
			}
			var batchUpdateRequest = new BatchUpdateValuesRequest
			{
				ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW.ToString(),
				Data = new List<ValueRange>() { actualRange, await RegisterLastUpdate() }
			};

			var request = AccountManager.SheetsService.Spreadsheets.Values.BatchUpdate(batchUpdateRequest, AccountManager.GSheet.Get<string>("SpreadsheetId"));
			await request.ExecuteAsync();
		}

		private async Task UpdateCell(List<ValueRange> actualRange)
		{
			if (!CheckINIparams())
			{
				Logger.Error("Google Sheet values cannot be updated.", new Exception(".INI file params is not setted"));
				return;
			}
			actualRange.Add(await RegisterLastUpdate());
			var batchUpdateRequest = new BatchUpdateValuesRequest
			{
				ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW.ToString(),
				Data = actualRange
			};

			var request = AccountManager.SheetsService.Spreadsheets.Values.BatchUpdate(batchUpdateRequest, AccountManager.GSheet.Get<string>("SpreadsheetId"));
			await request.ExecuteAsync();
		}

		private async Task<ValueRange> CreateAccountsTableRange(string cell, object data)
		{
			return new ValueRange
			{
				Range = $"{AccountManager.GSheet.Get<string>("AccountsTableName")}!{cell}",
				Values = new List<IList<object>> { new List<object> { data } }
			};
		}

		private async Task<ValueRange> RegisterLastUpdate() => await CreateAccountsTableRange($"{Columns["LastUpdate"]}{Row}", DateTime.Now.ToString("G"));

		private async Task UpdateToken(string token) => await UpdateCell(new List<ValueRange> {
			await CreateAccountsTableRange($"{Columns["Token"]}{Row}", token),
			await CreateAccountsTableRange($"{Columns["TokenCreatedTime"]}{Row}", DateTime.Now.ToString("G"))
		});
		private async Task UpdateState(string state) => await UpdateCell(await CreateAccountsTableRange($"{Columns["State"]}{Row}", state));
		private async Task UpdateStatus(string status) => await UpdateCell(await CreateAccountsTableRange($"{Columns["Status"]}{Row}", status));
	}
}
