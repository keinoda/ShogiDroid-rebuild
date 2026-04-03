using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;

namespace ShogiGUI.Engine;

/// <summary>
/// AWS EC2 スポットインスタンスの管理
/// </summary>
public class AwsSpotManager : IDisposable
{
	public const string TagKey = "ShogiDroid";
	public const string TagValue = "ngs43";
	public const string DefaultInstanceType = "c7a.metal-48xl";
	public const string KeyPairPrefix = "shogidroid-";
	public const string SecurityGroupName = "shogidroid-ssh";

	private AmazonEC2Client client_;
	private string region_;
	private bool disposed_;

	public AwsSpotManager(string accessKey, string secretKey, string region)
	{
		var credentials = new BasicAWSCredentials(accessKey, secretKey);
		var endpoint = RegionEndpoint.GetBySystemName(region);
		client_ = new AmazonEC2Client(credentials, endpoint);
		region_ = region;
	}

	public void Dispose()
	{
		if (!disposed_)
		{
			client_?.Dispose();
			disposed_ = true;
		}
	}

	// ── リソース準備 ──

	/// <summary>
	/// SSH公開鍵をキーペアとしてインポート。既に存在すればそのまま返す。
	/// </summary>
	public async Task<string> EnsureKeyPairAsync(string publicKeyPath, CancellationToken ct = default)
	{
		string keyName = KeyPairPrefix + "key";

		if (!File.Exists(publicKeyPath))
			throw new FileNotFoundException($"SSH公開鍵が見つかりません: {publicKeyPath}\n秘密鍵と同じ場所に .pub ファイルを配置してください");
		string pubKeyContent = File.ReadAllText(publicKeyPath).Trim();

		// shogidroid-key が既にあるか確認
		try
		{
			var desc = await client_.DescribeKeyPairsAsync(new DescribeKeyPairsRequest
			{
				KeyNames = new List<string> { keyName },
				IncludePublicKey = true
			}, ct);
			if (desc.KeyPairs.Count > 0)
			{
				var existing = desc.KeyPairs[0];
				// 公開鍵の内容が一致するか確認
				string existingPub = existing.PublicKey?.Trim() ?? "";
				// AWS は公開鍵を OpenSSH 形式で返すが、コメント部分が異なる場合があるため鍵本体で比較
				string localKeyBody = ExtractKeyBody(pubKeyContent);
				string remoteKeyBody = ExtractKeyBody(existingPub);
				if (localKeyBody == remoteKeyBody)
				{
					AppDebug.Log.Info($"AwsSpot: キーペア '{keyName}' は既に存在（公開鍵一致）");
					return keyName;
				}

				// 不一致: 古いキーペアを削除して再インポート
				AppDebug.Log.Info($"AwsSpot: キーペア '{keyName}' の公開鍵が不一致、再インポート");
				await client_.DeleteKeyPairAsync(new DeleteKeyPairRequest { KeyName = keyName }, ct);
			}
		}
		catch (AmazonEC2Exception ex) when (ex.ErrorCode == "InvalidKeyPair.NotFound")
		{
			// 存在しない → 新規インポート
		}

		// 公開鍵をインポート
		var importReq = new ImportKeyPairRequest
		{
			KeyName = keyName,
			PublicKeyMaterial = pubKeyContent
		};
		var result = await client_.ImportKeyPairAsync(importReq, ct);
		AppDebug.Log.Info($"AwsSpot: キーペア '{keyName}' をインポート: {result.KeyFingerprint}");
		return keyName;
	}

	/// <summary>
	/// OpenSSH 公開鍵文字列から鍵本体（Base64部分）を抽出
	/// </summary>
	private static string ExtractKeyBody(string pubKey)
	{
		if (string.IsNullOrEmpty(pubKey)) return "";
		var parts = pubKey.Split(' ');
		// "ssh-ed25519 AAAA... comment" → "AAAA..." 部分
		return parts.Length >= 2 ? parts[1] : pubKey;
	}

	/// <summary>
	/// SSH用セキュリティグループを作成。既に存在すればそのまま返す。
	/// </summary>
	public async Task<string> EnsureSecurityGroupAsync(CancellationToken ct = default)
	{
		// 既存チェック
		var descReq = new DescribeSecurityGroupsRequest
		{
			Filters = new List<Filter>
			{
				new Filter("group-name", new List<string> { SecurityGroupName })
			}
		};
		var descResp = await client_.DescribeSecurityGroupsAsync(descReq, ct);
		if (descResp.SecurityGroups.Count > 0)
		{
			string existingId = descResp.SecurityGroups[0].GroupId;
			AppDebug.Log.Info($"AwsSpot: SG '{SecurityGroupName}' は既に存在: {existingId}");
			return existingId;
		}

		// デフォルトVPCを取得
		var vpcs = await client_.DescribeVpcsAsync(new DescribeVpcsRequest
		{
			Filters = new List<Filter>
			{
				new Filter("is-default", new List<string> { "true" })
			}
		}, ct);
		if (vpcs.Vpcs.Count == 0)
			throw new InvalidOperationException($"リージョン {region_} にデフォルトVPCがありません");
		string vpcId = vpcs.Vpcs[0].VpcId;

		// SG作成
		var createResp = await client_.CreateSecurityGroupAsync(new CreateSecurityGroupRequest
		{
			GroupName = SecurityGroupName,
			Description = "ShogiDroid SSH access",
			VpcId = vpcId
		}, ct);
		string sgId = createResp.GroupId;

		// SSH (22) をインバウンド許可
		await client_.AuthorizeSecurityGroupIngressAsync(new AuthorizeSecurityGroupIngressRequest
		{
			GroupId = sgId,
			IpPermissions = new List<IpPermission>
			{
				new IpPermission
				{
					IpProtocol = "tcp",
					FromPort = 22,
					ToPort = 22,
					Ipv4Ranges = new List<IpRange>
					{
						new IpRange { CidrIp = "0.0.0.0/0", Description = "SSH" }
					}
				}
			}
		}, ct);

		AppDebug.Log.Info($"AwsSpot: SG '{SecurityGroupName}' を作成: {sgId}");
		return sgId;
	}

	/// <summary>
	/// 最新の Amazon Linux 2023 AMI を取得
	/// </summary>
	/// <summary>
	/// ShogiDroid カスタム AMI を検索。なければ素の Amazon Linux 2023 AMI を返す。
	/// </summary>
	public async Task<(string amiId, bool isCustom)> GetBestAmiAsync(CancellationToken ct = default)
	{
		// カスタム AMI（Docker プリインストール済み）を探す
		var customResp = await client_.DescribeImagesAsync(new DescribeImagesRequest
		{
			Owners = new List<string> { "self" },
			Filters = new List<Filter>
			{
				new Filter($"tag:{TagKey}", new List<string> { "ami" }),
				new Filter("state", new List<string> { "available" })
			}
		}, ct);

		var customAmi = customResp.Images?
			.OrderByDescending(i => i.CreationDate)
			.FirstOrDefault();
		if (customAmi != null)
		{
			AppDebug.Log.Info($"AwsSpot: カスタムAMI {customAmi.ImageId} ({customAmi.Name})");
			return (customAmi.ImageId, true);
		}

		// フォールバック: 素の Amazon Linux 2023
		var resp = await client_.DescribeImagesAsync(new DescribeImagesRequest
		{
			Owners = new List<string> { "amazon" },
			Filters = new List<Filter>
			{
				new Filter("name", new List<string> { "al2023-ami-2023*-x86_64" }),
				new Filter("state", new List<string> { "available" })
			}
		}, ct);

		var latest = resp.Images?
			.OrderByDescending(i => i.CreationDate)
			.FirstOrDefault();
		if (latest == null)
			throw new InvalidOperationException($"リージョン {region_} で AMI が見つかりません");

		AppDebug.Log.Info($"AwsSpot: ベースAMI {latest.ImageId} ({latest.Name})");
		return (latest.ImageId, false);
	}

	// ── Docker データボリューム ──

	/// <summary>
	/// 指定AZに既存の Docker データボリュームがあれば返す。なければ null。
	/// </summary>
	public async Task<string> FindDockerVolumeAsync(string availabilityZone, CancellationToken ct = default)
	{
		var resp = await client_.DescribeVolumesAsync(new DescribeVolumesRequest
		{
			Filters = new List<Filter>
			{
				new Filter($"tag:{TagKey}", new List<string> { "docker-data" }),
				new Filter("availability-zone", new List<string> { availabilityZone }),
				new Filter("status", new List<string> { "available" })
			}
		}, ct);

		var vol = resp.Volumes?.FirstOrDefault();
		if (vol != null)
		{
			AppDebug.Log.Info($"AwsSpot: 既存Dockerボリューム {vol.VolumeId} (AZ={availabilityZone})");
			return vol.VolumeId;
		}
		return null;
	}

	/// <summary>
	/// Docker データボリュームを作成
	/// </summary>
	public async Task<string> CreateDockerVolumeAsync(string availabilityZone, int sizeGb = 8, CancellationToken ct = default)
	{
		var resp = await client_.CreateVolumeAsync(new CreateVolumeRequest
		{
			AvailabilityZone = availabilityZone,
			Size = sizeGb,
			VolumeType = VolumeType.Gp3,
			TagSpecifications = new List<TagSpecification>
			{
				new TagSpecification
				{
					ResourceType = ResourceType.Volume,
					Tags = new List<Tag>
					{
						new Tag(TagKey, "docker-data"),
						new Tag("Name", "ShogiDroid-DockerData")
					}
				}
			}
		}, ct);

		string volId = resp.Volume?.VolumeId ?? resp.ResponseMetadata?.RequestId ?? "unknown";
		AppDebug.Log.Info($"AwsSpot: Dockerボリューム作成 {volId} ({sizeGb}GB, AZ={availabilityZone})");
		return volId;
	}

	/// <summary>
	/// ボリュームをインスタンスにアタッチ
	/// </summary>
	public async Task AttachVolumeAsync(string volumeId, string instanceId, string device = "/dev/xvdf", CancellationToken ct = default)
	{
		// ボリュームが available になるまで待機
		for (int i = 0; i < 30; i++)
		{
			var desc = await client_.DescribeVolumesAsync(new DescribeVolumesRequest
			{
				VolumeIds = new List<string> { volumeId }
			}, ct);
			if (desc.Volumes[0].State == VolumeState.Available)
				break;
			await Task.Delay(2000, ct);
		}

		await client_.AttachVolumeAsync(new AttachVolumeRequest
		{
			VolumeId = volumeId,
			InstanceId = instanceId,
			Device = device
		}, ct);

		AppDebug.Log.Info($"AwsSpot: ボリューム {volumeId} をアタッチ → {instanceId}");
	}

	// ── スポット価格 ──

	/// <summary>
	/// 指定インスタンスタイプのリージョン内スポット価格を取得（AZ別）
	/// </summary>
	public async Task<List<AwsSpotPrice>> GetSpotPricesAsync(string instanceType = DefaultInstanceType, CancellationToken ct = default)
	{
		var resp = await client_.DescribeSpotPriceHistoryAsync(new DescribeSpotPriceHistoryRequest
		{
			InstanceTypes = new List<string> { instanceType },
			ProductDescriptions = new List<string> { "Linux/UNIX" },
			MaxResults = 20
		}, ct);

		// AZごとに最新の価格のみ取得
		var prices = resp.SpotPriceHistory
			.GroupBy(s => s.AvailabilityZone)
			.Select(g => g.OrderByDescending(s => s.Timestamp).First())
			.OrderBy(s => decimal.Parse(s.Price))
			.Select(s => new AwsSpotPrice
			{
				AvailabilityZone = s.AvailabilityZone,
				Price = decimal.Parse(s.Price),
				Timestamp = s.Timestamp ?? DateTime.MinValue
			})
			.ToList();

		return prices;
	}

	// ── インスタンス起動 ──

	/// <summary>
	/// スポットインスタンスを起動
	/// </summary>
	public async Task<string> LaunchSpotInstanceAsync(AwsLaunchConfig config, CancellationToken ct = default)
	{
		string userData = BuildUserData(config.DockerImage, config.IsCustomAmi, config.AutoShutdownMinutes);
		string userDataBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(userData));

		var req = new RunInstancesRequest
		{
			ImageId = config.AmiId,
			InstanceType = InstanceType.FindValue(config.InstanceType),
			MinCount = 1,
			MaxCount = 1,
			KeyName = config.KeyPairName,
			SecurityGroupIds = new List<string> { config.SecurityGroupId },
			UserData = userDataBase64,
			// インスタンス内からの shutdown で terminate（課金停止）
			InstanceInitiatedShutdownBehavior = ShutdownBehavior.Terminate,
			InstanceMarketOptions = new InstanceMarketOptionsRequest
			{
				MarketType = MarketType.Spot,
				SpotOptions = new SpotMarketOptions
				{
					SpotInstanceType = SpotInstanceType.OneTime,
					InstanceInterruptionBehavior = InstanceInterruptionBehavior.Terminate
				}
			},
			TagSpecifications = new List<TagSpecification>
			{
				new TagSpecification
				{
					ResourceType = ResourceType.Instance,
					Tags = new List<Tag>
					{
						new Tag(TagKey, TagValue),
						new Tag("Name", $"ShogiDroid-NNUE")
					}
				}
			}
		};

		// AZ指定がある場合はサブネットを探す
		if (!string.IsNullOrEmpty(config.AvailabilityZone))
		{
			var subnets = await client_.DescribeSubnetsAsync(new DescribeSubnetsRequest
			{
				Filters = new List<Filter>
				{
					new Filter("availability-zone", new List<string> { config.AvailabilityZone }),
					new Filter("default-for-az", new List<string> { "true" })
				}
			}, ct);
			if (subnets.Subnets.Count > 0)
			{
				req.SubnetId = subnets.Subnets[0].SubnetId;
			}
		}

		var resp = await client_.RunInstancesAsync(req, ct);
		string instanceId = resp.Reservation.Instances[0].InstanceId;
		AppDebug.Log.Info($"AwsSpot: インスタンス起動: {instanceId} (AZ={config.AvailabilityZone})");
		return instanceId;
	}

	/// <summary>
	/// user data スクリプトを生成
	/// </summary>
	/// <summary>
	/// user data スクリプトを生成。カスタム AMI なら Docker インストール・SSH 設定をスキップ。
	/// </summary>
	private static string BuildUserData(string dockerImage, bool isCustomAmi, int autoShutdownMinutes)
	{
		return $@"#!/bin/bash
set -e
{(isCustomAmi ? "# カスタムAMI: Docker・SSH設定はプリインストール済み" : @"# Docker インストール
dnf install -y docker
# root SSH 有効化
mkdir -p /root/.ssh
cp /home/ec2-user/.ssh/authorized_keys /root/.ssh/authorized_keys
chmod 600 /root/.ssh/authorized_keys
sed -i 's/#PermitRootLogin.*/PermitRootLogin prohibit-password/' /etc/ssh/sshd_config
# docker-shell
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
SSHCONF")}
systemctl restart sshd

# Docker 起動
systemctl enable --now docker

# コンテナイメージ取得
docker pull {dockerImage}

# コンテナ起動
docker run -d --name shogidroid --restart=always {dockerImage} tail -f /dev/null

# 完了マーカー
echo SHOGIDROID_READY > /tmp/shogidroid_ready

# 安全装置: {autoShutdownMinutes}分後に自動シャットダウン（InstanceInitiatedShutdownBehavior=terminate で課金停止）
shutdown -h +{autoShutdownMinutes} &
";
	}

	// ── インスタンス状態 ──

	/// <summary>
	/// インスタンス情報を取得
	/// </summary>
	public async Task<AwsInstance> GetInstanceAsync(string instanceId, CancellationToken ct = default)
	{
		var resp = await client_.DescribeInstancesAsync(new DescribeInstancesRequest
		{
			InstanceIds = new List<string> { instanceId }
		}, ct);

		var inst = resp.Reservations.SelectMany(r => r.Instances).FirstOrDefault();
		if (inst == null) return null;

		return ToAwsInstance(inst);
	}

	/// <summary>
	/// インスタンスが "running" になるまで待機（ボリュームアタッチ用）
	/// </summary>
	public async Task<AwsInstance> WaitForRunningAsync(string instanceId, int maxRetries = 60, int intervalMs = 5000, CancellationToken ct = default)
	{
		for (int i = 0; i < maxRetries; i++)
		{
			ct.ThrowIfCancellationRequested();
			AwsInstance inst;
			try
			{
				inst = await GetInstanceAsync(instanceId, ct);
			}
			catch (Exception ex) when (ex is not System.OperationCanceledException)
			{
				AppDebug.Log.Info($"AwsSpot: WaitForRunning 一時エラー (リトライ {i}): {ex.Message}");
				await Task.Delay(intervalMs, ct);
				continue;
			}
			if (inst == null)
				throw new InvalidOperationException($"インスタンス {instanceId} が見つかりません");
			if (inst.State == "terminated" || inst.State == "shutting-down")
				throw new InvalidOperationException($"インスタンスが終了しました: {inst.State}");
			if (inst.State == "running" && !string.IsNullOrEmpty(inst.PublicIp))
			{
				AppDebug.Log.Info($"AwsSpot: インスタンス {instanceId} running ({inst.PublicIp})");
				return inst;
			}
			await Task.Delay(intervalMs, ct);
		}
		throw new TimeoutException($"インスタンス {instanceId} の起動がタイムアウトしました");
	}

	/// <summary>
	/// インスタンスが SSH 接続可能になるまで待機（一時的なネットワークエラーはリトライ）
	/// </summary>
	public async Task<AwsInstance> WaitForSshReadyAsync(string instanceId, int maxRetries = 120, int intervalMs = 5000, CancellationToken ct = default)
	{
		for (int i = 0; i < maxRetries; i++)
		{
			ct.ThrowIfCancellationRequested();
			AwsInstance inst;
			try
			{
				inst = await GetInstanceAsync(instanceId, ct);
			}
			catch (Exception ex) when (ex is not System.OperationCanceledException)
			{
				AppDebug.Log.Info($"AwsSpot: WaitForSsh 一時エラー (リトライ {i}): {ex.Message}");
				await Task.Delay(intervalMs, ct);
				continue;
			}
			if (inst == null || inst.State == "terminated" || inst.State == "shutting-down")
				throw new InvalidOperationException($"インスタンスが終了しました");

			if (inst.State == "running" && !string.IsNullOrEmpty(inst.PublicIp))
			{
				if (await IsSshReachableAsync(inst.PublicIp, 22, ct))
				{
					AppDebug.Log.Info($"AwsSpot: インスタンス {instanceId} SSH到達 ({inst.PublicIp})");
					return inst;
				}
			}
			await Task.Delay(intervalMs, ct);
		}
		throw new TimeoutException($"インスタンス {instanceId} のSSH到達がタイムアウトしました");
	}

	/// <summary>
	/// SSH ポートに TCP 接続できるか確認
	/// </summary>
	private static async Task<bool> IsSshReachableAsync(string host, int port, CancellationToken ct)
	{
		try
		{
			using var tcp = new System.Net.Sockets.TcpClient();
			var connectTask = tcp.ConnectAsync(host, port);
			var completed = await Task.WhenAny(connectTask, Task.Delay(3000, ct));
			return completed == connectTask && tcp.Connected;
		}
		catch
		{
			return false;
		}
	}

	// ── インスタンス終了 ──

	/// <summary>
	/// インスタンスを終了（削除）
	/// </summary>
	public async Task TerminateInstanceAsync(string instanceId, CancellationToken ct = default)
	{
		await client_.TerminateInstancesAsync(new TerminateInstancesRequest
		{
			InstanceIds = new List<string> { instanceId }
		}, ct);
		AppDebug.Log.Info($"AwsSpot: インスタンス終了: {instanceId}");
	}

	// ── インスタンス一覧 ──

	/// <summary>
	/// ShogiDroid タグ付きインスタンスを一覧取得
	/// </summary>
	public async Task<List<AwsInstance>> ListInstancesAsync(CancellationToken ct = default)
	{
		var resp = await client_.DescribeInstancesAsync(new DescribeInstancesRequest
		{
			Filters = new List<Filter>
			{
				new Filter($"tag:{TagKey}", new List<string> { TagValue }),
				// terminated は除外
				new Filter("instance-state-name", new List<string>
				{
					"pending", "running", "stopping", "stopped", "shutting-down"
				})
			}
		}, ct);

		if (resp.Reservations == null || resp.Reservations.Count == 0)
			return new List<AwsInstance>();

		return resp.Reservations
			.Where(r => r.Instances != null)
			.SelectMany(r => r.Instances)
			.Select(ToAwsInstance)
			.ToList();
	}

	// ── user data 完了チェック ──

	/// <summary>
	/// SSH経由で user data (Docker pull + コンテナ起動) が完了しているか確認。
	/// root は ForceCommand でコンテナに入るため、ec2-user で確認する。
	/// </summary>
	public static Task<bool> CheckReadyMarkerAsync(string host, string keyPath, CancellationToken ct)
	{
		try
		{
			using var keyFile = new Renci.SshNet.PrivateKeyFile(keyPath);
			using var ssh = new Renci.SshNet.SshClient(host, 22, "ec2-user", keyFile);
			ssh.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
			ssh.Connect();
			var cmd = ssh.RunCommand("cat /tmp/shogidroid_ready 2>/dev/null");
			ssh.Disconnect();
			return Task.FromResult(cmd.Result.Trim() == "SHOGIDROID_READY");
		}
		catch
		{
			return Task.FromResult(false);
		}
	}

	// ── ヘルパー ──

	private static AwsInstance ToAwsInstance(Instance inst)
	{
		string name = inst.Tags?.FirstOrDefault(t => t.Key == "Name")?.Value ?? "";
		return new AwsInstance
		{
			InstanceId = inst.InstanceId,
			State = inst.State.Name.Value,
			PublicIp = inst.PublicIpAddress ?? "",
			InstanceType = inst.InstanceType?.Value ?? "",
			AvailabilityZone = inst.Placement?.AvailabilityZone ?? "",
			LaunchTime = inst.LaunchTime ?? DateTime.MinValue,
			Name = name,
			VCpuCount = inst.CpuOptions?.CoreCount * inst.CpuOptions?.ThreadsPerCore ?? 0,
			MemoryMb = 0 // DescribeInstancesでは取得不可、タイプから推定
		};
	}
}

// ── データモデル ──

public class AwsSpotPrice
{
	public string AvailabilityZone { get; set; }
	public decimal Price { get; set; }
	public DateTime Timestamp { get; set; }
}

public class AwsLaunchConfig
{
	public string AmiId { get; set; }
	public string InstanceType { get; set; } = AwsSpotManager.DefaultInstanceType;
	public string KeyPairName { get; set; }
	public string SecurityGroupId { get; set; }
	public string AvailabilityZone { get; set; }
	public string DockerImage { get; set; } = "keinoda/shogi:v9.21nnue";
	public bool IsCustomAmi { get; set; }
	public int AutoShutdownMinutes { get; set; } = 60;
}

public class AwsInstance
{
	public string InstanceId { get; set; }
	public string State { get; set; }
	public string PublicIp { get; set; }
	public string InstanceType { get; set; }
	public string AvailabilityZone { get; set; }
	public DateTime LaunchTime { get; set; }
	public string Name { get; set; }
	public int VCpuCount { get; set; }
	public int MemoryMb { get; set; }

	public string StatusDisplay
	{
		get
		{
			return State switch
			{
				"pending" => "起動中",
				"running" => "稼働中",
				"stopping" => "停止中",
				"stopped" => "停止",
				"shutting-down" => "終了中",
				"terminated" => "終了済み",
				_ => State
			};
		}
	}
}
