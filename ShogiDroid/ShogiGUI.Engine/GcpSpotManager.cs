using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ShogiGUI.Engine;

/// <summary>
/// GCP Compute Engine Spot VM の管理（REST API + JWT 認証）
/// </summary>
public class GcpSpotManager : IDisposable
{
    public const string LabelKey = "shogidroid";
    public const string LabelValue = "ngs43";
    public const string FirewallRuleName = "shogidroid-allow-ssh";
    public const string InstancePrefix = "shogidroid-";

    private readonly HttpClient http_;
    private readonly string projectId_;
    private readonly string clientEmail_;
    private readonly RSA rsa_;
    private string accessToken_;
    private DateTime tokenExpiry_ = DateTime.MinValue;
    private readonly SemaphoreSlim tokenLock_ = new SemaphoreSlim(1, 1);
    private bool disposed_;

    private string BaseUrl => $"https://compute.googleapis.com/compute/v1/projects/{projectId_}";

    /// <summary>
    /// サービスアカウント JSON キーファイルから初期化
    /// </summary>
    public GcpSpotManager(string serviceAccountJsonPath)
    {
        if (!File.Exists(serviceAccountJsonPath))
            throw new FileNotFoundException($"GCPサービスアカウントキーが見つかりません: {serviceAccountJsonPath}");

        string json = File.ReadAllText(serviceAccountJsonPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        projectId_ = root.GetProperty("project_id").GetString()
            ?? throw new GcpException("project_id が JSON キーに含まれていません");
        clientEmail_ = root.GetProperty("client_email").GetString()
            ?? throw new GcpException("client_email が JSON キーに含まれていません");
        string privateKeyPem = root.GetProperty("private_key").GetString()
            ?? throw new GcpException("private_key が JSON キーに含まれていません");

        // PKCS#8 PEM から RSA 鍵を読み込み
        rsa_ = RSA.Create();
        string base64Key = privateKeyPem
            .Replace("-----BEGIN PRIVATE KEY-----", "")
            .Replace("-----END PRIVATE KEY-----", "")
            .Replace("\n", "")
            .Replace("\r", "")
            .Trim();
        byte[] keyBytes = Convert.FromBase64String(base64Key);
        rsa_.ImportPkcs8PrivateKey(keyBytes, out _);

        http_ = new HttpClient();
        AppDebug.Log.Info($"GcpSpot: プロジェクト '{projectId_}' で初期化");
    }

    public void Dispose()
    {
        if (!disposed_)
        {
            http_?.Dispose();
            rsa_?.Dispose();
            tokenLock_?.Dispose();
            disposed_ = true;
        }
    }

    // ── 認証 ──

    /// <summary>
    /// JWT を生成し、OAuth2 トークンエンドポイントでアクセストークンに交換する。
    /// キャッシュ済みで有効期限内ならそのまま返す。
    /// </summary>
    private async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (DateTime.UtcNow < tokenExpiry_ && accessToken_ != null)
            return accessToken_;

        await tokenLock_.WaitAsync(ct);
        try
        {
            // ダブルチェック
            if (DateTime.UtcNow < tokenExpiry_ && accessToken_ != null)
                return accessToken_;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string headerJson = "{\"alg\":\"RS256\",\"typ\":\"JWT\"}";
            string payloadJson = JsonSerializer.Serialize(new
            {
                iss = clientEmail_,
                scope = "https://www.googleapis.com/auth/cloud-platform",
                aud = "https://oauth2.googleapis.com/token",
                iat = now,
                exp = now + 3600
            });

            string headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
            string payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
            string unsignedToken = $"{headerB64}.{payloadB64}";

            byte[] signature = rsa_.SignData(
                Encoding.UTF8.GetBytes(unsignedToken),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            string signatureB64 = Base64UrlEncode(signature);
            string jwt = $"{unsignedToken}.{signatureB64}";

            // トークン交換
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = jwt
            });
            var resp = await http_.PostAsync("https://oauth2.googleapis.com/token", content, ct);
            string body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new GcpException($"トークン取得失敗 ({resp.StatusCode}): {body}");

            using var tokenDoc = JsonDocument.Parse(body);
            accessToken_ = tokenDoc.RootElement.GetProperty("access_token").GetString();
            int expiresIn = tokenDoc.RootElement.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600;
            // 5分前に期限切れとみなして早めにリフレッシュ
            tokenExpiry_ = DateTime.UtcNow.AddSeconds(expiresIn - 300);

            AppDebug.Log.Info("GcpSpot: アクセストークン取得完了");
            return accessToken_;
        }
        finally
        {
            tokenLock_.Release();
        }
    }

    /// <summary>
    /// Base64url エンコード（パディングなし）
    /// </summary>
    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    // ── HTTP ヘルパー ──

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string url, CancellationToken ct)
    {
        string token = await GetAccessTokenAsync(ct);
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    /// <summary>
    /// API リクエストを送信し、レスポンスボディを JsonDocument で返す
    /// </summary>
    private async Task<JsonDocument> SendAsync(HttpMethod method, string url, string jsonBody = null, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(method, url, ct);
        if (jsonBody != null)
        {
            req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        var resp = await http_.SendAsync(req, ct);
        string body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            // 404 は呼び出し元で個別処理するケースがあるため例外に含める
            throw new GcpException($"API エラー ({resp.StatusCode}) {method} {url}: {body}",
                (int)resp.StatusCode);
        }

        if (string.IsNullOrWhiteSpace(body))
            return null;

        return JsonDocument.Parse(body);
    }

    // ── ファイアウォール ──

    /// <summary>
    /// SSH 用ファイアウォールルールを作成。既に存在すればスキップ。
    /// </summary>
    public async Task EnsureFirewallRuleAsync(CancellationToken ct = default)
    {
        // 既存チェック
        try
        {
            await SendAsync(HttpMethod.Get, $"{BaseUrl}/global/firewalls/{FirewallRuleName}", ct: ct);
            AppDebug.Log.Info($"GcpSpot: ファイアウォール '{FirewallRuleName}' は既に存在");
            return;
        }
        catch (GcpException ex) when (ex.StatusCode == 404)
        {
            // 存在しない → 作成
        }

        var rule = new
        {
            name = FirewallRuleName,
            description = "ShogiDroid SSH access",
            direction = "INGRESS",
            priority = 1000,
            targetTags = new[] { "shogidroid" },
            allowed = new[]
            {
                new { IPProtocol = "tcp", ports = new[] { "22" } }
            },
            sourceRanges = new[] { "0.0.0.0/0" }
        };

        string ruleJson = JsonSerializer.Serialize(rule);
        await SendAsync(HttpMethod.Post, $"{BaseUrl}/global/firewalls", ruleJson, ct);
        AppDebug.Log.Info($"GcpSpot: ファイアウォール '{FirewallRuleName}' を作成");
    }

    // ── インスタンス作成 ──

    /// <summary>
    /// Spot VM を作成
    /// </summary>
    public async Task<string> CreateSpotInstanceAsync(GcpLaunchConfig config, CancellationToken ct = default)
    {
        string instanceName = $"{InstancePrefix}{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        string startupScript = BuildUserData(config.DockerImage, config.AutoShutdownMinutes);

        var instanceBody = new
        {
            name = instanceName,
            machineType = $"zones/{config.Zone}/machineTypes/{config.MachineType}",
            labels = new Dictionary<string, string>
            {
                [LabelKey] = LabelValue
            },
            tags = new
            {
                items = new[] { "shogidroid" }
            },
            disks = new[]
            {
                new
                {
                    boot = true,
                    initializeParams = new
                    {
                        sourceImage = "projects/debian-cloud/global/images/family/debian-12",
                        diskSizeGb = "10",
                        diskType = $"zones/{config.Zone}/diskTypes/pd-ssd"
                    },
                    autoDelete = true
                }
            },
            networkInterfaces = new[]
            {
                new
                {
                    network = "global/networks/default",
                    accessConfigs = new[]
                    {
                        new
                        {
                            name = "External NAT",
                            type = "ONE_TO_ONE_NAT"
                        }
                    }
                }
            },
            metadata = new
            {
                items = new[]
                {
                    new { key = "startup-script", value = startupScript },
                    new { key = "ssh-keys", value = $"root:{config.SshPublicKeyContent}" }
                }
            },
            scheduling = new
            {
                provisioningModel = "SPOT",
                instanceTerminationAction = "STOP",
                onHostMaintenance = "TERMINATE"
            }
        };

        string json = JsonSerializer.Serialize(instanceBody);
        string url = $"{BaseUrl}/zones/{config.Zone}/instances";
        await SendAsync(HttpMethod.Post, url, json, ct);

        AppDebug.Log.Info($"GcpSpot: インスタンス作成開始: {instanceName} (zone={config.Zone}, type={config.MachineType})");
        return instanceName;
    }

    /// <summary>
    /// スタートアップスクリプトを生成
    /// </summary>
    public static string BuildUserData(string dockerImage, int autoShutdownMinutes)
    {
        return $@"#!/bin/bash
set -e

# Docker インストール (Debian 12)
apt-get update -y
apt-get install -y ca-certificates curl gnupg
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/debian/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
chmod a+r /etc/apt/keyrings/docker.gpg
echo ""deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/debian $(. /etc/os-release && echo $VERSION_CODENAME) stable"" > /etc/apt/sources.list.d/docker.list
apt-get update -y
apt-get install -y docker-ce docker-ce-cli containerd.io

# root SSH 有効化（Debian のデフォルトユーザーから authorized_keys をコピー）
mkdir -p /root/.ssh
DEFAULT_USER=$(ls /home/ | head -1)
if [ -n ""$DEFAULT_USER"" ] && [ -f ""/home/$DEFAULT_USER/.ssh/authorized_keys"" ]; then
    cp ""/home/$DEFAULT_USER/.ssh/authorized_keys"" /root/.ssh/authorized_keys
    chmod 600 /root/.ssh/authorized_keys
fi
sed -i 's/#PermitRootLogin.*/PermitRootLogin prohibit-password/' /etc/ssh/sshd_config
sed -i 's/PermitRootLogin no/PermitRootLogin prohibit-password/' /etc/ssh/sshd_config

# docker-shell（root SSH で自動的にコンテナに入る）
cat > /usr/local/bin/docker-shell << 'DSHELL'
#!/bin/bash
if [ -n ""$SSH_ORIGINAL_COMMAND"" ]; then
    exec docker exec -i shogidroid bash -c ""$SSH_ORIGINAL_COMMAND""
else
    exec docker exec -it shogidroid bash
fi
DSHELL
chmod +x /usr/local/bin/docker-shell

cat >> /etc/ssh/sshd_config << 'SSHCONF'
Match User root
    ForceCommand /usr/local/bin/docker-shell
SSHCONF

systemctl restart sshd

# Docker 起動
systemctl enable --now docker

# コンテナイメージ取得・起動
docker pull {dockerImage}
docker run -d --name shogidroid --restart=always {dockerImage} tail -f /dev/null

# 完了マーカー（ホスト側 + コンテナ内の両方に書く）
echo SHOGIDROID_READY > /tmp/shogidroid_ready
docker exec shogidroid bash -c 'echo SHOGIDROID_READY > /tmp/shogidroid_ready'

# 安全装置: {autoShutdownMinutes}分後に自動シャットダウン
shutdown -h +{autoShutdownMinutes} &
";
    }

    // ── インスタンス一覧 ──

    /// <summary>
    /// ShogiDroid ラベル付きインスタンスを全ゾーンから取得
    /// </summary>
    public async Task<List<GcpInstance>> ListInstancesAsync(CancellationToken ct = default)
    {
        string filter = Uri.EscapeDataString($"labels.{LabelKey}={LabelValue}");
        string url = $"{BaseUrl}/aggregated/instances?filter={filter}";
        using var doc = await SendAsync(HttpMethod.Get, url, ct: ct);

        var result = new List<GcpInstance>();
        if (doc == null) return result;

        var root = doc.RootElement;
        if (!root.TryGetProperty("items", out var items))
            return result;

        foreach (var zoneProp in items.EnumerateObject())
        {
            if (!zoneProp.Value.TryGetProperty("instances", out var instances))
                continue;

            foreach (var inst in instances.EnumerateArray())
            {
                result.Add(ParseInstance(inst));
            }
        }

        return result;
    }

    // ── インスタンス取得 ──

    /// <summary>
    /// 指定ゾーン・名前のインスタンス情報を取得
    /// </summary>
    public async Task<GcpInstance> GetInstanceAsync(string zone, string instanceName, CancellationToken ct = default)
    {
        string url = $"{BaseUrl}/zones/{zone}/instances/{instanceName}";
        using var doc = await SendAsync(HttpMethod.Get, url, ct: ct);
        if (doc == null) return null;
        return ParseInstance(doc.RootElement);
    }

    // ── インスタンス操作 ──

    /// <summary>
    /// 停止中のインスタンスを起動
    /// </summary>
    public async Task StartInstanceAsync(string zone, string instanceName, CancellationToken ct = default)
    {
        string url = $"{BaseUrl}/zones/{zone}/instances/{instanceName}/start";
        await SendAsync(HttpMethod.Post, url, ct: ct);
        AppDebug.Log.Info($"GcpSpot: インスタンス起動: {instanceName}");
    }

    /// <summary>
    /// インスタンスを停止
    /// </summary>
    public async Task StopInstanceAsync(string zone, string instanceName, CancellationToken ct = default)
    {
        string url = $"{BaseUrl}/zones/{zone}/instances/{instanceName}/stop";
        await SendAsync(HttpMethod.Post, url, ct: ct);
        AppDebug.Log.Info($"GcpSpot: インスタンス停止: {instanceName}");
    }

    /// <summary>
    /// インスタンスを削除
    /// </summary>
    public async Task DeleteInstanceAsync(string zone, string instanceName, CancellationToken ct = default)
    {
        string url = $"{BaseUrl}/zones/{zone}/instances/{instanceName}";
        await SendAsync(HttpMethod.Delete, url, ct: ct);
        AppDebug.Log.Info($"GcpSpot: インスタンス削除: {instanceName}");
    }

    // ── 待機 ──

    /// <summary>
    /// インスタンスが RUNNING になり外部 IP が付与されるまでポーリング
    /// </summary>
    public async Task<GcpInstance> WaitForRunningAsync(string zone, string instanceName, int maxRetries = 60, int intervalMs = 5000, CancellationToken ct = default)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            ct.ThrowIfCancellationRequested();
            GcpInstance inst;
            try
            {
                inst = await GetInstanceAsync(zone, instanceName, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                AppDebug.Log.Info($"GcpSpot: WaitForRunning 一時エラー (リトライ {i}): {ex.Message}");
                await Task.Delay(intervalMs, ct);
                continue;
            }

            if (inst == null)
                throw new GcpException($"インスタンス {instanceName} が見つかりません");
            if (inst.Status == "TERMINATED")
                throw new GcpException($"インスタンスが終了しました: {inst.Status}");
            if (inst.IsRunning && !string.IsNullOrEmpty(inst.ExternalIp))
            {
                AppDebug.Log.Info($"GcpSpot: インスタンス {instanceName} RUNNING ({inst.ExternalIp})");
                return inst;
            }
            await Task.Delay(intervalMs, ct);
        }
        throw new TimeoutException($"インスタンス {instanceName} の起動がタイムアウトしました");
    }

    /// <summary>
    /// SSH 接続可能かつ完了マーカーが存在するまでポーリング
    /// </summary>
    public async Task<GcpInstance> WaitForSshReadyAsync(string zone, string instanceName, string keyPath, int maxRetries = 120, int intervalMs = 5000, CancellationToken ct = default)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            ct.ThrowIfCancellationRequested();
            GcpInstance inst;
            try
            {
                inst = await GetInstanceAsync(zone, instanceName, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                AppDebug.Log.Info($"GcpSpot: WaitForSsh 一時エラー (リトライ {i}): {ex.Message}");
                await Task.Delay(intervalMs, ct);
                continue;
            }

            if (inst == null || inst.Status == "TERMINATED")
                throw new GcpException("インスタンスが終了しました");

            if (inst.IsRunning && !string.IsNullOrEmpty(inst.ExternalIp))
            {
                if (await CheckReadyMarkerAsync(inst.ExternalIp, keyPath, ct))
                {
                    AppDebug.Log.Info($"GcpSpot: インスタンス {instanceName} SSH準備完了 ({inst.ExternalIp})");
                    return inst;
                }
            }
            await Task.Delay(intervalMs, ct);
        }
        throw new TimeoutException($"インスタンス {instanceName} のSSH準備完了がタイムアウトしました");
    }

    /// <summary>
    /// SSH 経由で完了マーカーを確認。
    /// root は ForceCommand でコンテナ内に入るため、コンテナ内の /tmp/shogidroid_ready を確認する。
    /// </summary>
    public static Task<bool> CheckReadyMarkerAsync(string host, string keyPath, CancellationToken ct)
    {
        try
        {
            using var keyFile = new Renci.SshNet.PrivateKeyFile(keyPath);
            using var ssh = new Renci.SshNet.SshClient(host, 22, "root", keyFile);
            ssh.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
            ssh.Connect();
            var cmd = ssh.RunCommand("cat /tmp/shogidroid_ready 2>/dev/null");
            ssh.Disconnect();
            return Task.FromResult(cmd.Result.Trim() == "SHOGIDROID_READY");
        }
        catch (Exception ex)
        {
            AppDebug.Log.Info($"GcpSpot: SSH到達確認失敗 ({host}): {ex.Message}");
            return Task.FromResult(false);
        }
    }

    // ── Spot 価格取得 ──

    /// <summary>
    /// Cloud Billing Catalog API から Spot vCPU/RAM 単価を取得し、
    /// 指定マシンタイプの時間あたり料金を計算して返す。
    /// </summary>
    public async Task<Dictionary<string, double>> GetSpotPricingAsync(
        string region, string[] machineTypes, CancellationToken ct = default)
    {
        var result = new Dictionary<string, double>();
        try
        {
            // Compute Engine のサービスID
            const string computeServiceId = "6F81-5844-456A";
            // Billing Catalog API で Spot Preemptible の SKU を取得
            var spotCpuPrices = new Dictionary<string, double>(); // family → $/vCPU/h
            var spotRamPrices = new Dictionary<string, double>(); // family → $/GB/h

            string nextPageToken = "";
            int pages = 0;
            const int maxPages = 20;

            do
            {
                string url = $"https://cloudbilling.googleapis.com/v1/services/{computeServiceId}/skus?currencyCode=USD&pageSize=5000";
                if (!string.IsNullOrEmpty(nextPageToken))
                    url += $"&pageToken={nextPageToken}";

                using var doc = await SendAsync(HttpMethod.Get, url, null, ct);
                if (doc == null) break;
                var root = doc.RootElement;

                if (root.TryGetProperty("skus", out var skus))
                {
                    foreach (var sku in skus.EnumerateArray())
                    {
                        string desc = sku.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                        string descLower = desc.ToLowerInvariant();

                        // Spot Preemptible の vCPU/RAM SKU のみ対象
                        if (!descLower.Contains("spot preemptible")) continue;

                        // 対象ファミリーのフィルタ
                        bool isC3d = descLower.Contains("c3d");
                        bool isC4d = descLower.Contains("c4d");
                        if (!isC3d && !isC4d) continue;

                        string family = isC3d ? "c3d" : "c4d";
                        bool isCpu = descLower.Contains("core") || descLower.Contains("cpu") || descLower.Contains("vcpu");
                        bool isRam = descLower.Contains("ram") || descLower.Contains("memory");
                        if (!isCpu && !isRam) continue;

                        // リージョン判定: serviceRegions に対象リージョンが含まれるか厳密に確認
                        bool regionMatch = false;
                        if (sku.TryGetProperty("serviceRegions", out var regions))
                        {
                            for (int r = 0; r < regions.GetArrayLength(); r++)
                            {
                                string sr = regions[r].GetString() ?? "";
                                // 完全一致（例: "us-central1" == "us-central1"）
                                if (sr == region) { regionMatch = true; break; }
                                // グローバルSKU
                                if (sr == "global") { regionMatch = true; break; }
                            }
                        }
                        if (!regionMatch) continue;

                        // 単価を抽出
                        double unitPrice = ExtractUnitPrice(sku);
                        if (unitPrice <= 0) continue;

                        string key = family;
                        if (isCpu && (!spotCpuPrices.ContainsKey(key) || unitPrice < spotCpuPrices[key]))
                            spotCpuPrices[key] = unitPrice;
                        if (isRam && (!spotRamPrices.ContainsKey(key) || unitPrice < spotRamPrices[key]))
                            spotRamPrices[key] = unitPrice;
                    }
                }

                nextPageToken = root.TryGetProperty("nextPageToken", out var npt) ? npt.GetString() ?? "" : "";
                pages++;
            }
            while (!string.IsNullOrEmpty(nextPageToken) && pages < maxPages);

            // マシンタイプごとに価格を計算
            foreach (string mt in machineTypes)
            {
                var parts = mt.Split('-');
                if (parts.Length < 3) continue;
                string family = parts[0];
                string tier = parts[1];
                if (!int.TryParse(parts[2], out int vCpus)) continue;

                // highcpu: 2GB/vCPU, standard: 4GB/vCPU, highmem: 8GB/vCPU
                int ramGb = tier switch
                {
                    "highcpu" => vCpus * 2,
                    "highmem" => vCpus * 8,
                    _ => vCpus * 4
                };

                if (spotCpuPrices.TryGetValue(family, out double cpuPrice) &&
                    spotRamPrices.TryGetValue(family, out double ramPrice))
                {
                    double totalPrice = cpuPrice * vCpus + ramPrice * ramGb;
                    result[mt] = totalPrice;
                    AppDebug.Log.Info($"GcpSpot価格: {mt} = ${totalPrice:F4}/h (CPU ${cpuPrice:F6}*{vCpus} + RAM ${ramPrice:F6}*{ramGb})");
                }
            }
        }
        catch (Exception ex)
        {
            AppDebug.Log.Info($"GcpSpot: 価格取得エラー: {ex.Message}");
        }
        return result;
    }

    /// <summary>
    /// SKU の pricingInfo から単価 ($/unit/hour) を抽出
    /// </summary>
    private static double ExtractUnitPrice(JsonElement sku)
    {
        try
        {
            if (!sku.TryGetProperty("pricingInfo", out var pricingInfo) || pricingInfo.GetArrayLength() == 0)
                return 0;
            var pi = pricingInfo[0];
            if (!pi.TryGetProperty("pricingExpression", out var pe))
                return 0;
            if (!pe.TryGetProperty("tieredRates", out var rates) || rates.GetArrayLength() == 0)
                return 0;
            // 最後のティア（通常1つだけ）
            var rate = rates[rates.GetArrayLength() - 1];
            if (!rate.TryGetProperty("unitPrice", out var unitPrice))
                return 0;
            long units = unitPrice.TryGetProperty("units", out var u) ? (long.TryParse(u.GetRawText().Trim('"'), out long ul) ? ul : 0) : 0;
            int nanos = unitPrice.TryGetProperty("nanos", out var na) ? na.GetInt32() : 0;
            return units + nanos / 1_000_000_000.0;
        }
        catch (Exception ex)
        {
            AppDebug.Log.Info($"GcpSpot: SKU価格パース失敗: {ex.Message}");
            return 0;
        }
    }

    // ── ヘルパー ──

    /// <summary>
    /// API レスポンスの JSON からインスタンス情報をパース
    /// </summary>
    private static GcpInstance ParseInstance(JsonElement elem)
    {
        string name = elem.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";

        // zone は "projects/.../zones/us-central1-a" 形式 → 最後の部分を取得
        string zoneRaw = elem.TryGetProperty("zone", out var z) ? z.GetString() ?? "" : "";
        string zone = zoneRaw.Contains('/') ? zoneRaw.Substring(zoneRaw.LastIndexOf('/') + 1) : zoneRaw;

        string status = elem.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";

        // machineType も "zones/.../machineTypes/n1-standard-4" 形式
        string machineTypeRaw = elem.TryGetProperty("machineType", out var mt) ? mt.GetString() ?? "" : "";
        string machineType = machineTypeRaw.Contains('/') ? machineTypeRaw.Substring(machineTypeRaw.LastIndexOf('/') + 1) : machineTypeRaw;

        string creationTimestamp = elem.TryGetProperty("creationTimestamp", out var ct) ? ct.GetString() ?? "" : "";

        // 外部 IP: networkInterfaces[0].accessConfigs[0].natIP
        string externalIp = "";
        if (elem.TryGetProperty("networkInterfaces", out var nis) && nis.GetArrayLength() > 0)
        {
            var ni = nis[0];
            if (ni.TryGetProperty("accessConfigs", out var acs) && acs.GetArrayLength() > 0)
            {
                var ac = acs[0];
                if (ac.TryGetProperty("natIP", out var ip))
                    externalIp = ip.GetString() ?? "";
            }
        }

        return new GcpInstance
        {
            Name = name,
            Zone = zone,
            Status = status,
            ExternalIp = externalIp,
            MachineType = machineType,
            CreationTimestamp = creationTimestamp
        };
    }
}

// ── データモデル ──

/// <summary>
/// GCP Spot VM 起動設定
/// </summary>
public class GcpLaunchConfig
{
    public string Zone { get; set; } = "us-central1-a";
    public string MachineType { get; set; } = "c2-standard-60";
    public string DockerImage { get; set; } = "keinoda/shogi:v9.21nnue";
    public int AutoShutdownMinutes { get; set; } = 60;
    /// <summary>
    /// SSH 公開鍵の内容（例: "ssh-ed25519 AAAA..."）
    /// </summary>
    public string SshPublicKeyContent { get; set; }
}

/// <summary>
/// GCP インスタンス情報
/// </summary>
public class GcpInstance
{
    public string Name { get; set; }
    public string Zone { get; set; }
    public string Status { get; set; }
    public string ExternalIp { get; set; }
    public string MachineType { get; set; }
    public string CreationTimestamp { get; set; }

    public bool IsRunning => Status == "RUNNING";
    public bool IsStopped => Status == "TERMINATED" || Status == "STOPPED";

    public string StatusDisplay
    {
        get
        {
            return Status switch
            {
                "PROVISIONING" => "準備中",
                "STAGING" => "起動準備中",
                "RUNNING" => "稼働中",
                "STOPPING" => "停止中",
                "STOPPED" => "停止",
                "SUSPENDING" => "サスペンド中",
                "SUSPENDED" => "サスペンド",
                "TERMINATED" => "終了済み",
                _ => Status
            };
        }
    }
}

/// <summary>
/// GCP API 操作の例外
/// </summary>
public class GcpException : Exception
{
    public int StatusCode { get; }

    public GcpException(string message) : base(message) { }

    public GcpException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public GcpException(string message, Exception innerException) : base(message, innerException) { }
}
