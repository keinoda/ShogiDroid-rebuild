using System.IO;
using System.Text.Json;

namespace ShogiLib;

public static class DeepCopyHelper
{
	public static T DeepCopy<T>(T target)
	{
		var type = target.GetType();
		var json = JsonSerializer.Serialize(target, type);
		return (T)JsonSerializer.Deserialize(json, type);
	}
}
