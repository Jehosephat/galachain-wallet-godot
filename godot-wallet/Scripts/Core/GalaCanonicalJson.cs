using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;

namespace GalaWallet.Core;

public static class GalaCanonicalJson
{
	public static string Serialize(object value)
	{
		object normalized = Normalize(value, isRoot: true);
		return JsonSerializer.Serialize(normalized);
	}

	private static readonly HashSet<string> ExcludedFields = new(StringComparer.OrdinalIgnoreCase)
	{
		"signature",
		"trace"
	};

	private static object? Normalize(object? value, bool isRoot = false)
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
			string camelName = prop.Name.Substring(0, 1).ToLower() + prop.Name.Substring(1);

			if (isRoot && ExcludedFields.Contains(camelName))
				continue;

			dict[camelName] = Normalize(prop.GetValue(value));
		}

		return dict;
	}
}
