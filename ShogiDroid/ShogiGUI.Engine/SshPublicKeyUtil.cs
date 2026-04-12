using System;
using System.IO;
using System.Linq;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Security;

namespace ShogiGUI.Engine;

/// <summary>
/// SSH 公開鍵を .pub から正規化して読み込む。自動生成は行わない。
/// </summary>
public static class SshPublicKeyUtil
{
	private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

	public static string EnsurePublicKey(string privateKeyPath, string publicKeyPath)
	{
		if (string.IsNullOrEmpty(privateKeyPath))
			throw new FileNotFoundException("SSH秘密鍵パスが空です");
		if (!File.Exists(privateKeyPath))
			throw new FileNotFoundException($"SSH秘密鍵が見つかりません: {privateKeyPath}");

		// 自動生成機能は一旦無効化。公開鍵ファイルが無い/読めない場合はエラーにする。
		if (string.IsNullOrEmpty(publicKeyPath) || !File.Exists(publicKeyPath))
			throw new FileNotFoundException($"SSH公開鍵が見つかりません: {publicKeyPath}。アプリ設定のリモート接続設定から公開鍵をインポートしてください。");

		string normalized = NormalizePublicKey(File.ReadAllText(publicKeyPath));

		string dir = Path.GetDirectoryName(publicKeyPath);
		if (!string.IsNullOrEmpty(dir))
			Directory.CreateDirectory(dir);

		string existing = File.ReadAllText(publicKeyPath).Replace("\uFEFF", "").Trim();
		if (!string.Equals(existing, normalized, StringComparison.Ordinal))
		{
			// 旧インポート処理でファイルに read-only 属性がついていると
			// File.WriteAllText が UnauthorizedAccessException を出すため、
			// 書き戻す前に属性をクリアする。失敗した場合は正規化済み文字列だけ返す。
			try
			{
				var attrs = File.GetAttributes(publicKeyPath);
				if ((attrs & FileAttributes.ReadOnly) != 0)
					File.SetAttributes(publicKeyPath, attrs & ~FileAttributes.ReadOnly);
				File.WriteAllText(publicKeyPath, normalized + Environment.NewLine, Utf8NoBom);
			}
			catch (Exception ex)
			{
				AppDebug.Log.Info($"SshPublicKey: 正規化内容の書き戻しに失敗（続行）: {ex.Message}");
			}
		}

		return normalized;
	}

	public static string NormalizePublicKey(string publicKeyContent)
	{
		if (string.IsNullOrWhiteSpace(publicKeyContent))
			throw new InvalidDataException("SSH公開鍵が空です");

		string normalized = publicKeyContent.Replace("\uFEFF", "").Trim();
		string[] parts = normalized.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 2)
			throw new InvalidDataException("SSH公開鍵の形式が不正です");

		byte[] keyData = Convert.FromBase64String(parts[1]);
		string algorithm = new SshKeyData(keyData).Name;
		if (string.IsNullOrEmpty(algorithm))
			throw new InvalidDataException("SSH公開鍵のアルゴリズムを解釈できません");

		if (algorithm.Any(ch => ch > 0x7F) || parts[1].Any(ch => ch > 0x7F))
			throw new InvalidDataException("SSH公開鍵に非 ASCII 文字が含まれています");

		return $"{algorithm} {parts[1]}";
	}

	private static string DerivePublicKey(string privateKeyPath)
	{
		using var keyFile = new PrivateKeyFile(privateKeyPath);
		byte[] keyData = keyFile.HostKeyAlgorithms.FirstOrDefault()?.Data
			?? throw new InvalidDataException("秘密鍵から公開鍵を導出できません");

		string algorithm = new SshKeyData(keyData).Name;
		if (string.IsNullOrEmpty(algorithm))
			throw new InvalidDataException("秘密鍵から公開鍵アルゴリズムを解釈できません");

		string encoded = Convert.ToBase64String(keyData);
		return $"{algorithm} {encoded}";
	}
}
