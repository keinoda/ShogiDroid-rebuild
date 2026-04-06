using System;
using System.Diagnostics;

namespace AppDebug;

public static class Log
{
	private const string TAG = "ShogiDroid";

	private static long starttime;

	public static void Initialize()
	{
		Android.Util.Log.Info(TAG, "Log initialized");
	}

	private static string GetCallerInfo()
	{
		var frame = new StackTrace(fNeedFileInfo: true).GetFrame(2);
		return frame?.GetMethod()?.DeclaringType?.Name + "." + frame?.GetMethod()?.Name;
	}

	public static void Fatal(string str)
	{
		var caller = GetCallerInfo() ?? "?";
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
		var caller = GetCallerInfo() ?? "?";
		Android.Util.Log.Error(TAG, $"[ERROR] {caller}: {str}");
	}

	public static void Warning(string str)
	{
		var caller = GetCallerInfo() ?? "?";
		Android.Util.Log.Warn(TAG, $"[WARN] {caller}: {str}");
	}

	public static void Info(string str)
	{
		Android.Util.Log.Info(TAG, $"[INFO] {str}");
	}

	[Conditional("DEBUG")]
	public static void Dbg(string str)
	{
		var caller = GetCallerInfo() ?? "?";
		Android.Util.Log.Debug(TAG, $"[DBG] {caller}: {str}");
	}

	[Conditional("TRACE_ON")]
	public static void Trace(string str)
	{
		var caller = GetCallerInfo() ?? "?";
		Android.Util.Log.Verbose(TAG, $"[TRACE] {caller}: {str}");
	}

	public static void StartTime()
	{
		starttime = DateTime.Now.Ticks;
	}

	public static void PrintTime()
	{
		var caller = GetCallerInfo() ?? "?";
		long elapsed = (DateTime.Now.Ticks - starttime) / TimeSpan.TicksPerMillisecond;
		Android.Util.Log.Debug(TAG, $"[TIME] {caller}: {elapsed}ms");
	}
}
