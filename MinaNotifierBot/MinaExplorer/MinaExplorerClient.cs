using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace MinaNotifierBot.MinaExplorer
{
	internal class MinaExplorerClient
	{
		ILogger<MinaExplorerClient> _logger;
		HttpClient _client;

		private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings {
			ContractResolver = new CamelCasePropertyNamesContractResolver()
		};

		public MinaExplorerClient(HttpClient http, ILogger<MinaExplorerClient> logger, IConfiguration config)
		{
			_client = http;
			_client.BaseAddress = new Uri(config.GetValue<string>("MinaExplorerUrl"));
			_logger = logger;
		}

		string Download(string addr)
		{
			try
			{
				object result;
				_logger.LogDebug($"download {_client.BaseAddress}{addr}");
				result = _client.GetStringAsync(addr).ConfigureAwait(false).GetAwaiter().GetResult();
				_logger.LogDebug($"download complete: {_client.BaseAddress}{addr}");
				return (string)result;
			}
			catch
			{
				Thread.Sleep(3000);
				try
				{
					object result = _client.GetStringAsync(addr).ConfigureAwait(false).GetAwaiter().GetResult();
					_logger.LogDebug($"download complete: {_client.BaseAddress}{addr}");
					return (string)result;
				}
				catch (Exception e)
				{
					_logger.LogError(e, $"Error downloading from {_client.BaseAddress}{addr}");
					throw;
				}
			}
		}
		T? Download<T>(string path)
		{
			var result = Download(path);
			try
			{
				return JsonConvert.DeserializeObject<T>(result);
			}
			catch (Exception e)
			{
				var type = typeof(T);
				_logger.LogError(e, "Failed to deserialize result of request {Path} to {Type}", path, type);
				return default;
			}
		}

		public Account? GetAccount(string addr)
		{
			return Download<AccountResult>($"accounts/{addr}")?.account;
		}

		public Block? GetBlock(int maxHeight)
		{
			var br = Download<BlocksResult>("blocks")?.blocks.First();
			if (br?.blockHeight > maxHeight && maxHeight > 0)
			{
				var response = _client.PostAsJsonAsync("https://graphql.minaexplorer.com/", new { query = "query MyQuery {\n  block(query: {canonical: true, blockHeight: " + maxHeight.ToString() + "}) {\n    stateHash\n  }\n}\n", variables = (string?)null, operationName = "MyQuery" }).ConfigureAwait(true).GetAwaiter().GetResult();
				if (response.StatusCode == System.Net.HttpStatusCode.OK)
				{
					string result = response.Content.ReadAsStringAsync().ConfigureAwait(true).GetAwaiter().GetResult();
					var hash = JsonConvert.DeserializeObject<Root>(result)?.data?.block?.stateHash;
					if (hash != null)
						return Download<Block>("blocks/" + hash);
				}
			}
			else if (br != null)
				br.last = true;
			return br;
		}

		public class RootDataBlock
		{
			public string stateHash { get; set; } = "";
		}

		public class RootData
		{
			public RootDataBlock? block { get; set; }
		}

		public class Root
		{
			public RootData? data { get; set; }
		}
	}
}
