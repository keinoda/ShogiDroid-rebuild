using System;
using System.Diagnostics;
using System.Threading;

namespace AppDebug;

public static class Log
{
	private const string TAG = "ShogiDroid";

	private static SynchronizationContext syncContext;

	private static long starttime;

	public static void Initialize()
	{
		syncContext = SynchronizationContext.Current;
		Android.Util.Log.Info(TAG, "Log initialized");
	}

	public static void Fatal(string str)
	{
		var frame = new StackTrace(fNeedFileInfo: true).GetFrame(1);
		var caller = frame != null ? $"{frame.GetMethod()?.DeclaringType?.Name}.{frame.GetMethod()?.Name}" : "?";
		Android.Util.Log.Error(TAG, $"[FATAL] {caller}: {str}");
	}

	public static void ErrorException(Exception e)
	{
		Android.Util.Log.Error(TAG, $"[EXCEPTION] {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
	}

	public static void ErrorException(Exception e, string msg)
	{
		Android.Util.Log.Error(TAG, $"[EXCEPTION] {msg}: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
	}

	public static void Error(string str)
	{
		var frame = new StackTrace(fNeedFileInfo: true).GetFrame(1);
		var caller = frame != null ? $"{frame.GetMethod()?.DeclaringType?.Name}.{frame.GetMethod()?.Name}" : "?";
		Android.Util.Log.Error(TAG, $"[ERROR] {caller}: {str}");
	}

	public static void Warning(string str)
	{
		var frame = new StackTrace(fNeedFileInfo: true).GetFrame(1);
		var caller = frame != null ? $"{frame.GetMethod()?.DeclaringType?.Name}.{frame.GetMethod()?.Name}" : "?";
		Android.Util.Log.Warn(TAG, $"[WARN] {caller}: {str}");
	}

	public static void Info(string str)
	{
		Android.Util.Log.Info(TAG, $"[INFO] {str}");
	}

	[Conditional("DEBUG")]
	public static void Dbg(string str)
	{
		var frame = new StackTrace(fNeedFileInfo: true).GetFrame(1);
		var caller = frame != null ? $"{frame.GetMethod()?.DeclaringType?.Name}.{frame.GetMethod()?.Name}" : "?";
		Android.Util.Log.Debug(TAG, $"[DBG] {caller}: {str}");
	}

	[Conditional("TRACE_ON")]
	public static void Trace(string str)
	{
		var frame = new StackTrace(fNeedFileInfo: true).GetFrame(1);
		var caller = frame != null ? $"{frame.GetMethod()?.DeclaringType?.Name}.{frame.GetMethod()?.Name}" : "?";
		Android.Util.Log.Verbose(TAG, $"[TRACE] {caller}: {str}");
	}

	public static void StartTime()
	{
		starttime = DateTime.Now.Ticks;
	}

	public static void PrintTime()
	{
		var frame = new StackTrace(fNeedFileInfo: true).GetFrame(1);
		var caller = frame != null ? $"{frame.GetMethod()?.DeclaringType?.Name}.{frame.GetMethod()?.Name}" : "?";
		long elapsed = (DateTime.Now.Ticks - starttime) / TimeSpan.TicksPerMillisecond;
		Android.Util.Log.Debug(TAG, $"[TIME] {caller}: {elapsed}ms");
	}
}
