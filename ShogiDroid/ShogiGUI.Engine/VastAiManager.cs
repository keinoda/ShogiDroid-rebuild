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
	public const string ShogiLabel = "ShogiDroidR";
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

		string json = JsonSerializer.Serialize(body);
		AppDebug.Log.Info($"VastAi: creating instance from offer {offerId}");

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

		var instance = JsonSerializer.Deserialize<VastAiInstance>(body);
		return instance;
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
			string status = instance?.ActualStatus ?? "unknown";

			progress?.Report($"ステータス: {status} ({attempt * 5}秒経過)");
			AppDebug.Log.Info($"VastAi: instance {instanceId} status={status}, attempt={attempt}");

			if (status == "running" && !string.IsNullOrEmpty(instance.PublicIpAddr))
			{
				AppDebug.Log.Info($"VastAi: instance ready! IP={instance.PublicIpAddr}");
				return instance;
			}

			if (status == "exited" || status == "error" || status == "offline")
			{
				throw new VastAiException($"インスタンスが異常終了しました: {status}");
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
		var query = new Dictionary<string, object>
		{
			["rentable"] = new Dictionary<string, object> { ["eq"] = true },
			["rented"] = new Dictionary<string, object> { ["eq"] = false },
			["order"] = new object[] { new object[] { "dph_total", "asc" } },
			["type"] = "bid"
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
	public string[] GpuNames = new[] { "RTX 4090", "RTX 4090 D" };
	public int MinCpuCoresEffective = 32;
	public double MaxDphTotal = 0.4;
	public int MinGpuRam = 0;
}

public class VastAiInstanceConfig
{
	public string DockerImage = "keinoda/shogi:v9.0";
	public int[] Ports = new[] { 6000, 6001 };
	public double DiskGb = 8.0;
	public string OnStartCmd = "";
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
	public bool IsStopped => ActualStatus == "exited" || IntendedStatus == "stopped";
	public bool IsLoading => ActualStatus == "loading" || ActualStatus == "created";

	public string StatusDisplay
	{
		get
		{
			if (IsRunning) return "稼働中";
			if (IsStopped) return "休止中";
			if (IsLoading) return "起動中";
			return ActualStatus ?? "不明";
		}
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
		$"CPU: {CpuCoresEffective:F0}cores (割当)\n" +
		$"RAM: {CpuRamGb:F0}GB\n" +
		$"コスト: ${DphTotal:F3}/h";
}

public class VastAiPortMapping
{
	[JsonPropertyName("HostPort")]
	public string HostPort { get; set; }
}

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
