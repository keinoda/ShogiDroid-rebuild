using System;
using System.Reflection;
using Android.Content;

namespace ShogiGUI;

public class PrefSerializer
{
	public void Serialize(ISharedPreferences pref, object obj)
	{
		ISharedPreferencesEditor sharedPreferencesEditor = pref.Edit();
		do_serialize(sharedPreferencesEditor, obj, string.Empty);
		sharedPreferencesEditor.Commit();
	}

	private void do_serialize(ISharedPreferencesEditor editor, object obj, string prefix)
	{
		FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
		foreach (FieldInfo fieldInfo in fields)
		{
			string text = prefix + ((prefix == string.Empty) ? string.Empty : ".") + fieldInfo.Name;
			if (fieldInfo.FieldType.IsSerializable)
			{
				if (fieldInfo.FieldType.IsEnum)
				{
					object value = fieldInfo.GetValue(obj);
					editor.PutString(text, value.ToString());
				}
				else if (fieldInfo.FieldType.Name == "Boolean")
				{
					editor.PutBoolean(text, (bool)fieldInfo.GetValue(obj));
				}
				else if (fieldInfo.FieldType.Name == "String")
				{
					editor.PutString(text, (string)fieldInfo.GetValue(obj));
				}
				else if (fieldInfo.FieldType.Name == "Single")
				{
					editor.PutFloat(text, (float)fieldInfo.GetValue(obj));
				}
				else if (fieldInfo.FieldType.IsValueType)
				{
					editor.PutInt(text, (int)fieldInfo.GetValue(obj));
				}
			}
			else if (fieldInfo.FieldType.IsClass)
			{
				do_serialize(editor, fieldInfo.GetValue(obj), text);
			}
		}
	}

	public void Deserialize(ISharedPreferences pref, object obj)
	{
		do_deserialize(pref, obj, string.Empty);
	}

	private void do_deserialize(ISharedPreferences pref, object obj, string prefix)
	{
		FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
		foreach (FieldInfo fieldInfo in fields)
		{
			string text = prefix + ((prefix == string.Empty) ? string.Empty : ".") + fieldInfo.Name;
			if (fieldInfo.FieldType.IsSerializable)
			{
				if (fieldInfo.FieldType.IsEnum)
				{
					object value = fieldInfo.GetValue(obj);
					string value2 = pref.GetString(text, value.ToString());
					try
					{
						fieldInfo.SetValue(obj, Enum.Parse(fieldInfo.FieldType, value2));
					}
					catch
					{
					}
				}
				else if (fieldInfo.FieldType.Name == "Boolean")
				{
					bool boolean = pref.GetBoolean(text, (bool)fieldInfo.GetValue(obj));
					fieldInfo.SetValue(obj, boolean);
				}
				else if (fieldInfo.FieldType.Name == "String")
				{
					string value3 = pref.GetString(text, (string)fieldInfo.GetValue(obj));
					fieldInfo.SetValue(obj, value3);
				}
				else if (fieldInfo.FieldType.Name == "Single")
				{
					float num = pref.GetFloat(text, (float)fieldInfo.GetValue(obj));
					fieldInfo.SetValue(obj, num);
				}
				else if (fieldInfo.FieldType.IsValueType)
				{
					int num2 = pref.GetInt(text, (int)fieldInfo.GetValue(obj));
					fieldInfo.SetValue(obj, num2);
				}
			}
			else if (fieldInfo.FieldType.IsClass)
			{
				do_deserialize(pref, fieldInfo.GetValue(obj), text);
			}
		}
	}
}
