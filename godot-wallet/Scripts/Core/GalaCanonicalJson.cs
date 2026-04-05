using System.Collections;
using System.Collections.Generic;
using System.Text.Json;

public static class GalaCanonicalJson
{
	public static string Serialize(object value)
	{
		object normalized = Normalize(value);
		return JsonSerializer.Serialize(normalized);
	}

	private static object? Normalize(object? value)
	{
		if (value == null) return null;

		var type = value.GetType();

		if (value is string || type.IsPrimitive || value is decimal)
			return value;

		if (value is IEnumerable enumerable && value is not string)
		{
			var list = new List<object?>();
			foreach (var item in enumerable)
			{
				list.Add(Normalize(item));
			}
			return list;
		}

		var dict = new SortedDictionary<string, object?>();
		foreach (var prop in type.GetProperties())
		{
			var propValue = prop.GetValue(value);
			dict[prop.Name.Substring(0, 1).ToLower() + prop.Name.Substring(1)] = Normalize(propValue);
		}

		return dict;
	}
}
