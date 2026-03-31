using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ShogiGUI.Engine;

/// <summary>
/// vast.ai REST API client for managing cloud GPU instances.
/// </summary>
public class VastAiManager : IDisposable
{
	private const string BaseUrl = "https://console.vast.ai/api/v0";
	public const string ShogiLabel = "ShogiDroid-ngs43";
	private readonly HttpClient httpClient_;
	private string apiKey_;
	private int? currentInstanceId_;

	public int? CurrentInstanceId => currentInstanceId_;
	public bool HasRunningInstance => currentInstanceId_.HasValue;

	public VastAiManager(string apiKey)
	{
		apiKey_ = apiKey;
		httpClient_ = new HttpClient();
		httpClient_.Timeout = TimeSpan.FromSeconds(30);
	}

	public void Dispose()
	{
		httpClient_?.Dispose();
	}

	/// <summary>
	/// Search for available offers (interruptible only).
	/// </summary>
	public async Task<List<VastAiOffer>> SearchOffersAsync(VastAiSearchCriteria criteria, CancellationToken ct = default)
	{
		var query = BuildSearchQuery(criteria);
		string queryJson = JsonSerializer.Serialize(query);
		string url = $"{BaseUrl}/bundles?q={Uri.EscapeDataString(queryJson)}&api_key={apiKey_}";

		AppDebug.Log.Info($"VastAi: searching offers: {queryJson}");

		var response = await httpClient_.GetAsync(url, ct);
		string body = await response.Content.ReadAsStringAsync();

		if (!response.IsSuccessStatusCode)
		{
			AppDebug.Log.Error($"VastAi: search failed: {response.StatusCode} {body}");
			throw new VastAiException($"オファー検索に失敗しました: {response.StatusCode}");
		}

		var result = JsonSerializer.Deserialize<VastAiSearchResult>(body);
		if (result?.Offers == null)
		{
			return new List<VastAiOffer>();
		}

		AppDebug.Log.Info($"VastAi: found {result.Offers.Count} offers");
		return result.Offers.OrderBy(o => o.DphTotal).ToList();
	}

	/// <summary>
	/// Create a new instance from an offer with shogi label.
	/// </summary>
	public async Task<int> CreateInstanceAsync(int offerId, VastAiInstanceConfig config, CancellationToken ct = default)
	{
		string url = $"{BaseUrl}/asks/{offerId}/?api_key={apiKey_}";

		var body = new Dictionary<string, object>
		{
			["client_id"] = "me",
			["image"] = config.DockerImage,
			["env"] = BuildEnvDict(config.Ports),
			["onstart"] = config.OnStartCmd,
			["disk"] = config.DiskGb,
			["label"] = ShogiLabel,
			["runtype"] = "ssh_direc"
		};

		// interruptible（bid）モード：入札価格を指定
		if (config.BidPrice > 0)
		{
			body["price"] = config.BidPrice;
		}

		string json = JsonSerializer.Serialize(body);
		string mode = config.BidPrice > 0 ? $"interruptible (${config.BidPrice:F3}/h)" : "on-demand";
		AppDebug.Log.Info($"VastAi: creating instance from offer {offerId}, mode={mode}");

		var content = new StringContent(json, Encoding.UTF8, "application/json");
		var response = await httpClient_.PutAsync(url, content, ct);
		string responseBody = await response.Content.ReadAsStringAsync();

		if (!response.IsSuccessStatusCode)
		{
			AppDebug.Log.Error($"VastAi: create instance failed: {response.StatusCode} {responseBody}");
			throw new VastAiException($"インスタンス作成に失敗しました: {response.StatusCode}");
		}

		var result = JsonSerializer.Deserialize<VastAiCreateResult>(responseBody);
		if (result == null || !result.Success)
		{
			throw new VastAiException("インスタンス作成に失敗しました");
		}

		currentInstanceId_ = result.NewContract;
		AppDebug.Log.Info($"VastAi: instance created, id={currentInstanceId_}");
		return currentInstanceId_.Value;
	}

	/// <summary>
	/// Get instance details including status, IP, and port mappings.
	/// </summary>
	public async Task<VastAiInstance> GetInstanceAsync(int instanceId, CancellationToken ct = default)
	{
		string url = $"{BaseUrl}/instances/{instanceId}?api_key={apiKey_}";

		var response = await httpClient_.GetAsync(url, ct);
		string body = await response.Content.ReadAsStringAsync();

		if (!response.IsSuccessStatusCode)
		{
			AppDebug.Log.Error($"VastAi: get instance failed: {response.StatusCode}");
			throw new VastAiException($"インスタンス情報の取得に失敗しました: {response.StatusCode}");
		}

		// API は {"instances": {…}} 形式で返す
		var wrapper = JsonSerializer.Deserialize<VastAiInstanceWrapper>(body);
		return wrapper?.Instance;
	}

	/// <summary>
	/// Wait for instance to be running and return its details.
	/// </summary>
	public async Task<VastAiInstance> WaitForReadyAsync(int instanceId, IProgress<string> progress = null, CancellationToken ct = default)
	{
		AppDebug.Log.Info($"VastAi: waiting for instance {instanceId} to be ready");
		int attempt = 0;
		const int maxAttempts = 120;

		while (attempt < maxAttempts)
		{
			ct.ThrowIfCancellationRequested();

			var instance = await GetInstanceAsync(instanceId, ct);
			string actual = instance?.ActualStatus ?? "unknown";
			string intended = instance?.IntendedStatus ?? "unknown";

			progress?.Report($"ステータス: {actual} (intended: {intended}, {attempt * 5}秒経過)");
			AppDebug.Log.Info($"VastAi: instance {instanceId} actual={actual}, intended={intended}, attempt={attempt}");

			if (actual == "running" && !string.IsNullOrEmpty(instance.PublicIpAddr))
			{
				AppDebug.Log.Info($"VastAi: instance ready! IP={instance.PublicIpAddr}");
				return instance;
			}

			if (instance != null && instance.HasStartupFailure)
			{
				throw new VastAiException($"インスタンスが異常終了しました: {actual} (intended: {intended})");
			}

			await Task.Delay(5000, ct);
			attempt++;
		}

		throw new VastAiException("インスタンスの起動がタイムアウトしました");
	}

	/// <summary>
	/// Set or clear label on an instance.
	/// </summary>
	public async Task LabelInstanceAsync(int instanceId, string label, CancellationToken ct = default)
	{
		string url = $"{BaseUrl}/instances/{instanceId}/?api_key={apiKey_}";

		var body = new Dictionary<string, object> { ["label"] = label };
		string json = JsonSerializer.Serialize(body);

		AppDebug.Log.Info($"VastAi: labeling instance {instanceId} as '{label}'");

		var content = new StringContent(json, Encoding.UTF8, "application/json");
		var response = await httpClient_.PutAsync(url, content, ct);

		if (!response.IsSuccessStatusCode)
		{
			AppDebug.Log.Error($"VastAi: label failed: {response.StatusCode}");
		}
	}

	/// <summary>
	/// Start (resume) a stopped instance.
	/// </summary>
	public async Task StartInstanceAsync(int instanceId, CancellationToken ct = default)
	{
		string url = $"{BaseUrl}/instances/{instanceId}/?api_key={apiKey_}";

		var body = new Dictionary<string, object> { ["state"] = "running" };
		string json = JsonSerializer.Serialize(body);

		AppDebug.Log.Info($"VastAi: starting instance {instanceId}");

		var content = new StringContent(json, Encoding.UTF8, "application/json");
		var response = await httpClient_.PutAsync(url, content, ct);
		string responseBody = await response.Content.ReadAsStringAsync();

		if (!response.IsSuccessStatusCode)
		{
			AppDebug.Log.Error($"VastAi: start failed: {response.StatusCode} {responseBody}");
			throw new VastAiException($"インスタンスの再開に失敗しました: {response.StatusCode}");
		}

		currentInstanceId_ = instanceId;
	}

	/// <summary>
	/// Stop (pause) an instance. Data is preserved.
	/// </summary>
	public async Task StopInstanceAsync(int instanceId, CancellationToken ct = default)
	{
		string url = $"{BaseUrl}/instances/{instanceId}/?api_key={apiKey_}";

		var body = new Dictionary<string, object> { ["state"] = "stopped" };
		string json = JsonSerializer.Serialize(body);

		AppDebug.Log.Info($"VastAi: stopping instance {instanceId}");

		var content = new StringContent(json, Encoding.UTF8, "application/json");
		var response = await httpClient_.PutAsync(url, content, ct);

		if (!response.IsSuccessStatusCode)
		{
			AppDebug.Log.Error($"VastAi: stop failed: {response.StatusCode}");
			throw new VastAiException($"インスタンスの一時停止に失敗しました: {response.StatusCode}");
		}
	}

	/// <summary>
	/// Destroy (delete) an instance. Removes label first.
	/// </summary>
	public async Task DestroyInstanceAsync(int instanceId, CancellationToken ct = default)
	{
		// Remove shogi label before destroying
		try { await LabelInstanceAsync(instanceId, "", ct); } catch { }

		string url = $"{BaseUrl}/instances/{instanceId}/?api_key={apiKey_}";

		AppDebug.Log.Info($"VastAi: destroying instance {instanceId}");

		var request = new HttpRequestMessage(HttpMethod.Delete, url);
		var response = await httpClient_.SendAsync(request, ct);

		if (!response.IsSuccessStatusCode)
		{
			string body = await response.Content.ReadAsStringAsync();
			AppDebug.Log.Error($"VastAi: destroy failed: {response.StatusCode} {body}");
			throw new VastAiException($"インスタンス削除に失敗しました: {response.StatusCode}");
		}

		if (currentInstanceId_ == instanceId)
		{
			currentInstanceId_ = null;
		}

		AppDebug.Log.Info($"VastAi: instance {instanceId} destroyed");
	}

	/// <summary>
	/// List all instances (running, stopped, loading, etc.).
	/// </summary>
	public async Task<List<VastAiInstance>> ListInstancesAsync(CancellationToken ct = default)
	{
		string url = $"{BaseUrl}/instances?api_key={apiKey_}";
		var response = await httpClient_.GetAsync(url, ct);
		string body = await response.Content.ReadAsStringAsync();

		if (!response.IsSuccessStatusCode)
		{
			throw new VastAiException($"インスタンス一覧の取得に失敗しました: {response.StatusCode}");
		}

		var result = JsonSerializer.Deserialize<VastAiInstanceList>(body);
		return result?.Instances ?? new List<VastAiInstance>();
	}

	/// <summary>
	/// ユーザーのクレジット残高を取得する
	/// </summary>
	public async Task<double?> GetCreditBalanceAsync(CancellationToken ct = default)
	{
		try
		{
			string url = $"{BaseUrl}/users/current/?api_key={apiKey_}";
			var response = await httpClient_.GetAsync(url, ct);
			string body = await response.Content.ReadAsStringAsync();
			if (!response.IsSuccessStatusCode) return null;

			using var doc = JsonDocument.Parse(body);
			if (doc.RootElement.TryGetProperty("credit", out var credit))
			{
				return credit.GetDouble();
			}
			if (doc.RootElement.TryGetProperty("balance", out var balance))
			{
				return balance.GetDouble();
			}
			if (doc.RootElement.TryGetProperty("balance_threshold_enabled", out _) &&
				doc.RootElement.TryGetProperty("balance_threshold", out var threshold))
			{
				return threshold.GetDouble();
			}
		}
		catch { }
		return null;
	}

	public void SetApiKey(string apiKey)
	{
		apiKey_ = apiKey;
	}

	public void SetCurrentInstanceId(int? id)
	{
		currentInstanceId_ = id;
	}

	private Dictionary<string, object> BuildSearchQuery(VastAiSearchCriteria criteria)
	{
		string sortField = string.IsNullOrEmpty(criteria.SortField) ? "dph_total" : criteria.SortField;
		string sortDir = criteria.SortAsc ? "asc" : "desc";

		var query = new Dictionary<string, object>
		{
			["rentable"] = new Dictionary<string, object> { ["eq"] = true },
			["rented"] = new Dictionary<string, object> { ["eq"] = false },
			["order"] = new object[] { new object[] { sortField, sortDir } },
			["type"] = criteria.RentType ?? "bid"
		};

		if (criteria.GpuNames != null && criteria.GpuNames.Length > 0)
		{
			query["gpu_name"] = new Dictionary<string, object> { ["in"] = criteria.GpuNames };
		}

		if (criteria.MinCpuCoresEffective > 0)
		{
			query["cpu_cores_effective"] = new Dictionary<string, object> { ["gte"] = criteria.MinCpuCoresEffective };
		}

		if (criteria.MaxDphTotal > 0)
		{
			query["dph_total"] = new Dictionary<string, object> { ["lte"] = criteria.MaxDphTotal };
		}

		if (criteria.MinGpuRam > 0)
		{
			query["gpu_ram"] = new Dictionary<string, object> { ["gte"] = criteria.MinGpuRam * 1024 };
		}

		if (criteria.MinDiskSpace > 0)
		{
			query["disk_space"] = new Dictionary<string, object> { ["gte"] = (double)criteria.MinDiskSpace };
		}

		if (criteria.MinReliability > 0)
		{
			query["reliability2"] = new Dictionary<string, object> { ["gte"] = criteria.MinReliability / 100.0 };
		}

		if (criteria.MinInetDown > 0)
		{
			query["inet_down"] = new Dictionary<string, object> { ["gte"] = criteria.MinInetDown };
		}

		if (criteria.NumGpus > 0)
		{
			query["num_gpus"] = new Dictionary<string, object> { ["eq"] = criteria.NumGpus };
		}

		if (criteria.MinCudaVersion > 0)
		{
			query["cuda_max_good"] = new Dictionary<string, object> { ["gte"] = criteria.MinCudaVersion };
		}

		// 常にverifiedのみ、信頼度95%以上
		query["verified"] = new Dictionary<string, object> { ["eq"] = true };
		query["reliability2"] = new Dictionary<string, object> { ["gte"] = 0.95 };

		return query;
	}

	private Dictionary<string, string> BuildEnvDict(int[] ports)
	{
		var env = new Dictionary<string, string>();
		foreach (int port in ports)
		{
			env[$"-p {port}:{port}"] = "1";
		}
		return env;
	}
}

#region Data Models

public class VastAiSearchCriteria
{
	public string[] GpuNames = new[] { "RTX 4090", "RTX 5090" };
	public int MinCpuCoresEffective = 32;
	public double MaxDphTotal = 0.4;
	public int MinGpuRam = 0;       // GB
	public int MinDiskSpace = 0;     // GB
	public double MinReliability = 0; // % (0-100)
	public double MinInetDown = 0;   // Mbps
	public int NumGpus = 0;          // 0 = any
	public string SortField = "dph_total";
	public bool SortAsc = true;
	public string RentType = "bid"; // "bid" = interruptible, "on-demand" = on-demand
	public double MinCudaVersion = 0; // 例: 12.4
}

public class VastAiInstanceConfig
{
	public string DockerImage = "keinoda/shogi:v9.0";
	public int[] Ports = Array.Empty<int>();
	public double DiskGb = 8.0;
	public string OnStartCmd = "";
	/// <summary>
	/// 入札価格（$/GPU/h）。0より大きい場合interruptible（bid）モードで予約する。
	/// </summary>
	public double BidPrice = 0;
}

public class VastAiSearchResult
{
	[JsonPropertyName("offers")]
	public List<VastAiOffer> Offers { get; set; }
}

public class VastAiOffer
{
	[JsonPropertyName("id")]
	public int Id { get; set; }

	[JsonPropertyName("gpu_name")]
	public string GpuName { get; set; }

	[JsonPropertyName("num_gpus")]
	public int NumGpus { get; set; }

	[JsonPropertyName("gpu_ram")]
	public double GpuRam { get; set; }

	[JsonPropertyName("cpu_name")]
	public string CpuName { get; set; }

	[JsonPropertyName("cpu_cores")]
	public int CpuCores { get; set; }

	[JsonPropertyName("cpu_cores_effective")]
	public double CpuCoresEffective { get; set; }

	[JsonPropertyName("cpu_ram")]
	public double CpuRam { get; set; }

	[JsonPropertyName("disk_space")]
	public double DiskSpace { get; set; }

	[JsonPropertyName("dph_total")]
	public double DphTotal { get; set; }

	[JsonPropertyName("dlperf")]
	public double DlPerf { get; set; }

	[JsonPropertyName("inet_down")]
	public double InetDown { get; set; }

	[JsonPropertyName("inet_up")]
	public double InetUp { get; set; }

	[JsonPropertyName("reliability2")]
	public double Reliability { get; set; }

	[JsonPropertyName("geolocation")]
	public string Geolocation { get; set; }

	[JsonPropertyName("cuda_max_good")]
	public double CudaMaxGood { get; set; }

	[JsonPropertyName("verification")]
	public string Verification { get; set; }

	[JsonPropertyName("min_bid")]
	public double MinBid { get; set; }

	[JsonPropertyName("search_price")]
	public double SearchPrice { get; set; }

	public double GpuRamGb => GpuRam / 1024.0;
	public double CpuRamGb => CpuRam / 1024.0;
}

public class VastAiCreateResult
{
	[JsonPropertyName("success")]
	public bool Success { get; set; }

	[JsonPropertyName("new_contract")]
	public int NewContract { get; set; }
}

public class VastAiInstance
{
	[JsonPropertyName("id")]
	public int Id { get; set; }

	[JsonPropertyName("actual_status")]
	public string ActualStatus { get; set; }

	[JsonPropertyName("intended_status")]
	public string IntendedStatus { get; set; }

	[JsonPropertyName("status_msg")]
	public string StatusMsg { get; set; }

	[JsonPropertyName("label")]
	public string Label { get; set; }

	[JsonPropertyName("public_ipaddr")]
	public string PublicIpAddr { get; set; }

	[JsonPropertyName("ports")]
	public Dictionary<string, List<VastAiPortMapping>> Ports { get; set; }

	[JsonPropertyName("ssh_host")]
	public string SshHost { get; set; }

	[JsonPropertyName("ssh_port")]
	public int SshPort { get; set; }

	[JsonPropertyName("gpu_name")]
	public string GpuName { get; set; }

	[JsonPropertyName("num_gpus")]
	public int NumGpus { get; set; }

	[JsonPropertyName("gpu_ram")]
	public double GpuRam { get; set; }

	[JsonPropertyName("cpu_name")]
	public string CpuName { get; set; }

	[JsonPropertyName("cpu_cores")]
	public int CpuCores { get; set; }

	[JsonPropertyName("cpu_cores_effective")]
	public double CpuCoresEffective { get; set; }

	[JsonPropertyName("cpu_ram")]
	public double CpuRam { get; set; }

	[JsonPropertyName("dph_total")]
	public double DphTotal { get; set; }

	[JsonPropertyName("image_uuid")]
	public string ImageUuid { get; set; }

	[JsonPropertyName("cur_state")]
	public string CurState { get; set; }

	public double GpuRamGb => GpuRam / 1024.0;
	public double CpuRamGb => CpuRam / 1024.0;

	public bool IsShogiInstance => Label == VastAiManager.ShogiLabel;
	public bool IsRunning => ActualStatus == "running";
	public bool HasStartupFailure => ActualStatus == "error"
		|| ActualStatus == "offline"
		|| (ActualStatus == "exited" && IntendedStatus == "running");
	/// <summary>
	/// intended_status 基準で判定。actual_status は遅延するため信用しない。
	/// </summary>
	public bool IsStopped => IntendedStatus == "stopped"
		|| (ActualStatus == "exited" && IntendedStatus != "running");
	/// <summary>
	/// actual がまだ running でないが intended が running の場合は起動中。
	/// </summary>
	public bool IsLoading => !HasStartupFailure
		&& !IsStopped
		&& ((IntendedStatus == "running" && ActualStatus != "running")
			|| ActualStatus == "loading"
			|| ActualStatus == "created");

	public string StatusDisplay
	{
		get
		{
			if (ActualStatus == "running") return "稼働中";
			if (HasStartupFailure) return "エラー";
			if (IsLoading) return "起動中";
			if (IsStopped) return "休止中";
			if (ActualStatus == "error") return "エラー";
			return ActualStatus ?? "不明";
		}
	}

	/// <summary>
	/// SSH接続用のホストとポートを取得
	/// </summary>
	public (string Host, int Port) GetSshEndpoint()
	{
		string host = !string.IsNullOrEmpty(SshHost) ? SshHost : PublicIpAddr;
		int port = SshPort > 0 ? SshPort : GetMappedPort(22);
		return (host, port);
	}

	public int GetMappedPort(int containerPort)
	{
		if (Ports == null) return containerPort;

		string key = $"{containerPort}/tcp";
		if (Ports.TryGetValue(key, out var mappings) && mappings != null && mappings.Count > 0)
		{
			if (int.TryParse(mappings[0].HostPort, out int hp))
				return hp;
		}

		return containerPort;
	}

	public string SpecsSummary =>
		$"GPU: {GpuName} x{NumGpus} (VRAM {GpuRamGb:F0}GB)\n" +
		$"CPU: {CpuName} {CpuCoresEffective:F0}cores (割当)\n" +
		$"RAM: {CpuRamGb:F0}GB\n" +
		$"コスト: ${DphTotal:F3}/h";
}

public class VastAiPortMapping
{
	[JsonPropertyName("HostPort")]
	public string HostPort { get; set; }
}

/// <summary>
/// GET /instances/{id} のレスポンス: {"instances": {…}}（単一オブジェクト）
/// </summary>
public class VastAiInstanceWrapper
{
	[JsonPropertyName("instances")]
	public VastAiInstance Instance { get; set; }
}

/// <summary>
/// GET /instances のレスポンス: {"instances": [{…}, …]}（配列）
/// </summary>
public class VastAiInstanceList
{
	[JsonPropertyName("instances")]
	public List<VastAiInstance> Instances { get; set; }
}

public class VastAiException : Exception
{
	public VastAiException(string message) : base(message) { }
}

#endregion
