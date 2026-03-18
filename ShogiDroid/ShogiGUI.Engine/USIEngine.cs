using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace ShogiGUI.Engine;

public class USIEngine : IDisposable
{
	private USIOptions options_ = new USIOptions();

	private Process process_;

	private StringQueue string_queue_;

	private bool initialized_;

	private object lockObj = new object();

	private bool isRemote_;

	private TcpClient tcpClient_;

	private StreamWriter tcpWriter_;

	private Thread tcpReceiveThread_;

	public USIOptions Options => options_;

	public void Dispose()
	{
		lock (lockObj)
		{
			if (string_queue_ != null)
			{
				string_queue_.Dispose();
			}
			if (process_ != null)
			{
				process_.Dispose();
			}
			if (tcpClient_ != null)
			{
				try { tcpClient_.Close(); } catch { }
				tcpClient_ = null;
			}
			string_queue_ = null;
			process_ = null;
		}
	}

	public bool Initialize(string filename, string workingdirectory = null)
	{
		bool result = true;
		if (workingdirectory == null)
		{
			workingdirectory = Path.GetDirectoryName(filename);
		}
		AppDebug.Log.Info($"USIEngine.Initialize: filename={filename}, workingdir={workingdirectory}");
		lock (lockObj)
		{
			if (initialized_)
			{
				return false;
			}
			isRemote_ = false;
			string_queue_ = new StringQueue();
			process_ = new Process();
			bool isScript = IsShellScript(filename);
			if (isScript)
			{
				process_.StartInfo.FileName = "/system/bin/sh";
				process_.StartInfo.Arguments = filename;
				AppDebug.Log.Info($"USIEngine: shell script detected, using /system/bin/sh {filename}");
			}
			else
			{
				string linker64 = "/system/bin/linker64";
				string linker = "/system/bin/linker";
				bool useLinker = System.IO.File.Exists(linker64) || System.IO.File.Exists(linker);
				if (useLinker)
				{
					string linkerPath = System.IO.File.Exists(linker64) ? linker64 : linker;
					process_.StartInfo.FileName = linkerPath;
					process_.StartInfo.Arguments = filename;
					AppDebug.Log.Info($"USIEngine: using linker wrapper: {linkerPath} {filename}");
				}
				else
				{
					process_.StartInfo.FileName = filename;
				}
			}
			process_.StartInfo.CreateNoWindow = true;
			process_.StartInfo.UseShellExecute = false;
			process_.StartInfo.RedirectStandardInput = true;
			process_.StartInfo.RedirectStandardOutput = true;
			process_.StartInfo.WorkingDirectory = workingdirectory;
			process_.OutputDataReceived += process_DataRecieved;
			process_.EnableRaisingEvents = true;
			process_.Exited += process_Exited;
			try
			{
				AppDebug.Log.Info($"USIEngine: starting process: {filename}");
				process_.Start();
				AppDebug.Log.Info($"USIEngine: process started, pid={process_.Id}");
				process_.BeginOutputReadLine();
				try { process_.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
				process_.StandardInput.WriteLine(string.Empty);
				initialized_ = true;
			}
			catch (Exception ex)
			{
				AppDebug.Log.ErrorException(ex, $"USIEngine: failed to start process: {filename}");
				process_.Dispose();
				process_ = null;
				string_queue_.Dispose();
				string_queue_ = null;
				result = false;
			}
		}
		return result;
	}

	public bool InitializeRemote(string host, int port)
	{
		AppDebug.Log.Info($"USIEngine.InitializeRemote: host={host}, port={port}");
		lock (lockObj)
		{
			if (initialized_)
			{
				return false;
			}
			isRemote_ = true;
			string_queue_ = new StringQueue();
			try
			{
				tcpClient_ = new TcpClient();
				tcpClient_.Connect(host, port);
				var stream = tcpClient_.GetStream();
				var reader = new StreamReader(stream);
				tcpWriter_ = new StreamWriter(stream) { AutoFlush = true };

				tcpReceiveThread_ = new Thread(() =>
				{
					try
					{
						string line;
						while ((line = reader.ReadLine()) != null)
						{
							lock (lockObj)
							{
								if (string_queue_ != null)
									string_queue_.Push(line.Replace("\r", string.Empty));
							}
						}
					}
					catch (Exception ex)
					{
						AppDebug.Log.Info($"USIEngine: TCP receive ended: {ex.Message}");
					}
					finally
					{
						lock (lockObj)
						{
							if (string_queue_ != null)
								string_queue_.Close();
						}
					}
				});
				tcpReceiveThread_.IsBackground = true;
				tcpReceiveThread_.Start();

				initialized_ = true;
				AppDebug.Log.Info($"USIEngine: TCP connected to {host}:{port}");
			}
			catch (Exception ex)
			{
				AppDebug.Log.ErrorException(ex, $"USIEngine: failed to connect to {host}:{port}");
				try { tcpClient_?.Close(); } catch { }
				tcpClient_ = null;
				tcpWriter_ = null;
				string_queue_.Dispose();
				string_queue_ = null;
				return false;
			}
		}
		return true;
	}

	public void Terminate()
	{
		if (!initialized_)
		{
			return;
		}
		initialized_ = false;
		string_queue_.Close();
		if (isRemote_)
		{
			lock (lockObj)
			{
				try { tcpClient_?.Close(); } catch { }
				tcpClient_ = null;
				tcpWriter_ = null;
				tcpReceiveThread_?.Join(5000);
				tcpReceiveThread_ = null;
				string_queue_.Close();
				string_queue_.Dispose();
				string_queue_ = null;
			}
		}
		else
		{
			if (!process_.WaitForExit(10000))
			{
				try
				{
					process_.Kill();
				}
				catch
				{
				}
			}
			lock (lockObj)
			{
				process_.OutputDataReceived -= process_DataRecieved;
				process_.Exited -= process_Exited;
				process_.Close();
				process_ = null;
				string_queue_.Close();
				string_queue_.Dispose();
				string_queue_ = null;
			}
		}
	}

	private void process_DataRecieved(object sendingProcess, DataReceivedEventArgs outLine)
	{
		lock (lockObj)
		{
			if (!string.IsNullOrEmpty(outLine.Data) && string_queue_ != null)
			{
				string_queue_.Push(outLine.Data.Replace("\r", string.Empty));
			}
		}
	}

	private void process_Exited(object sender, EventArgs e)
	{
		lock (lockObj)
		{
			if (string_queue_ != null)
			{
				string_queue_.Close();
			}
		}
	}

	public StringQueue.Error ReadLine(out string str, int timeout)
	{
		return string_queue_.Pop(out str, timeout);
	}

	public void WriteLine(string str)
	{
		try
		{
			if (isRemote_)
			{
				tcpWriter_.WriteLine(str);
				tcpWriter_.Flush();
			}
			else
			{
				process_.StandardInput.WriteLine(str);
				process_.StandardInput.Flush();
			}
		}
		catch
		{
		}
	}

	public bool HasOption(string name)
	{
		return options_.ContainsKey(name);
	}

	public string GetOptionValue(string name)
	{
		if (options_.ContainsKey(name))
		{
			return options_[name].ValueToString();
		}
		return string.Empty;
	}

	public bool SetOption(string name, bool value)
	{
		bool result = false;
		if (options_.ContainsKey(name))
		{
			result = options_[name].SetValue(value);
		}
		return result;
	}

	public bool SetOption(string name, int value)
	{
		bool result = false;
		if (options_.ContainsKey(name))
		{
			result = options_[name].SetValue(value);
		}
		return result;
	}

	public bool SetOption(string name, string value)
	{
		bool result = false;
		if (options_.ContainsKey(name))
		{
			result = options_[name].SetValue(value);
		}
		return result;
	}

	public bool AddOption(string str)
	{
		return options_.AddOption(str);
	}

	public void SetOptions(Dictionary<string, string> opt_name_value)
	{
		foreach (KeyValuePair<string, string> item in opt_name_value)
		{
			SetOption(item.Key, item.Value);
		}
	}

	private static bool IsShellScript(string filename)
	{
		try
		{
			using var fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
			byte[] header = new byte[2];
			if (fs.Read(header, 0, 2) == 2)
			{
				return header[0] == '#' && header[1] == '!';
			}
		}
		catch { }
		return false;
	}
}
