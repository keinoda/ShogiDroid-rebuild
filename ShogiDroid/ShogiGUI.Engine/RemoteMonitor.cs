using System;
using System.Threading;
using Renci.SshNet;

namespace ShogiGUI.Engine;

/// <summary>
/// SSH経由でリモートインスタンスのCPU/GPU利用率を定期取得。
/// 接続エラー時は自動再接続する。
/// </summary>
public class RemoteMonitor : IDisposable
{
	private SshClient client_;
	private Timer timer_;
	private bool disposed_;
	private int errorCount_;

	// 接続情報（再接続用に保持）
	private string host_;
	private int sshPort_;
	private string keyPath_;

	public double CpuUsage { get; private set; }
	public double GpuUsage { get; private set; }
	public bool IsMonitoring { get; private set; }

	/// <summary>
	/// CPU/GPU利用率が更新された時に発火
	/// </summary>
	public event Action<double, double> Updated;

	public void Start(string host, int sshPort, string keyPath)
	{
		Stop();
		host_ = host;
		sshPort_ = sshPort;
		keyPath_ = keyPath;
		IsMonitoring = true;
		errorCount_ = 0;
		timer_ = new Timer(Poll, null, 0, 2000);
		AppDebug.Log.Info($"RemoteMonitor: scheduled for {host}:{sshPort}");
	}

	public void Stop()
	{
		IsMonitoring = false;
		timer_?.Dispose();
		timer_ = null;
		Disconnect();
	}

	private void Disconnect()
	{
		try { client_?.Disconnect(); } catch { }
		client_ = null;
	}

	private bool EnsureConnected()
	{
		if (client_ != null && client_.IsConnected) return true;

		Disconnect();
		try
		{
			var keyFile = new PrivateKeyFile(keyPath_);
			client_ = new SshClient(host_, sshPort_, "root", keyFile);
			client_.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
			client_.Connect();
			errorCount_ = 0;
			AppDebug.Log.Info("RemoteMonitor: connected");
			return true;
		}
		catch (Exception ex)
		{
			errorCount_++;
			if (errorCount_ <= 3 || errorCount_ % 10 == 0)
				AppDebug.Log.Info($"RemoteMonitor: connect failed (#{errorCount_}): {ex.Message}");
			Disconnect();
			return false;
		}
	}

	private void Poll(object state)
	{
		if (!IsMonitoring || disposed_) return;
		if (!EnsureConnected()) return;

		try
		{
			// CPU利用率: topコマンドから取得（1回サンプリング）
			var cpuCmd = client_.RunCommand(
				"top -bn1 | head -3 | grep '%Cpu' | awk '{print 100-$8}'");
			string cpuStr = cpuCmd.Result?.Trim() ?? "";
			if (double.TryParse(cpuStr, out double cpu))
				CpuUsage = cpu;

			// GPU利用率: nvidia-smiから取得
			var gpuCmd = client_.RunCommand(
				"nvidia-smi --query-gpu=utilization.gpu --format=csv,noheader,nounits 2>/dev/null | head -1");
			string gpuStr = gpuCmd.Result?.Trim() ?? "";
			if (double.TryParse(gpuStr, out double gpu))
				GpuUsage = gpu;
			else
				GpuUsage = -1;

			errorCount_ = 0;
			Updated?.Invoke(CpuUsage, GpuUsage);
		}
		catch (Exception)
		{
			// 接続が切れた場合、次回Pollで再接続を試みる
			Disconnect();
		}
	}

	public void Dispose()
	{
		if (!disposed_)
		{
			disposed_ = true;
			Stop();
		}
	}
}
