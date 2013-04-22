// http://www.codeproject.com/Tips/406235/A-Simple-PList-Parser-in-Csharp
// (c) paladin_t.
// Modified to accomodate long ints.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Xml.Linq;

namespace TuneOut.Audio
{
	/// <summary>
	/// Represents an Apple Property List file.
	/// </summary>
	internal class PList : Dictionary<string, object>
	{
		/// <summary>
		/// Creates an wasEmpty property list.
		/// </summary>
		internal PList()
		{
		}

		/// <summary>
		/// Creates a property list from an XML document.
		/// </summary>
		/// <param name="doc">The XML document.</param>
		internal PList(XDocument doc)
		{
			Contract.Requires(doc != null);

			Load(doc);
		}

		public int GetIntOrDefault(string key)
		{
			object obj;
			this.TryGetValue(key, out obj);
			return obj is long ? (int)(long)obj : 0;
		}

		public long GetLongOrDefault(string key)
		{
			object obj;
			this.TryGetValue(key, out obj);
			return obj is long ? (long)obj : 0;
		}

		public T GetOrDefault<T>(string key) where T : class
		{
			object obj;
			this.TryGetValue(key, out obj);
			return obj as T;
		}

		public string GetStringOrDefault(string key)
		{
			object obj;
			this.TryGetValue(key, out obj);
			return obj as string;
		}

		/// <summary>
		/// Loads the root of the XML property list.
		/// </summary>
		/// <param name="doc">The XML document.</param>
		internal void Load(XDocument doc)
		{
			Contract.Requires(doc != null);

			Clear();

			XElement plist = doc.Element("plist");
			XElement dict = plist.Element("dict");

			var dictElements = dict.Elements();
			Parse(this, dictElements);
		}

		/// <summary>
		/// Parses the XML property list.
		/// </summary>
		/// <param name="dict">A PList in which the parsed elements will be output.</param>
		/// <param name="elements">A collection of the elements in the property list.</param>
		internal void Parse(PList dict, IEnumerable<XElement> elements)
		{
			Contract.Requires(dict != null);
			Contract.Requires(elements != null);

			for (int i = 0; i < elements.Count(); i += 2)
			{
				XElement key = elements.ElementAt(i);
				XElement val = elements.ElementAt(i + 1);

				dict[key.Value] = ParseValue(val);
			}
		}

		/// <summary>
		/// Parses an array element in the XML property list.
		/// </summary>
		/// <param name="elements">The collection of XML elements in an array.</param>
		/// <returns>A list comprised of the elements in the array.</returns>
		internal List<object> ParseArray(IEnumerable<XElement> elements)
		{
			Contract.Requires(elements != null);

			List<object> list = new List<object>();
			foreach (XElement e in elements)
			{
				object one = ParseValue(e);
				list.Add(one);
			}

			return list;
		}

		/// <summary>
		/// Parses an element in the XML property list.
		/// </summary>
		/// <param name="val">The XML element to parse.</param>
		/// <returns>The converted object representation of the element.</returns>
		private object ParseValue(XElement val)
		{
			Contract.Requires(val != null);

			switch (val.Name.ToString())
			{
				case "string":
					return val.Value;
				case "integer":
					return long.Parse(val.Value);
				case "real":
					return float.Parse(val.Value);
				case "true":
					return true;
				case "false":
					return false;
				case "dict":
					PList plist = new PList();
					Parse(plist, val.Elements());
					return plist;
				case "array":
					List<object> list = ParseArray(val.Elements());
					return list;
				case "date":
					return DateTime.Parse(val.Value);
				case "data":
					return Convert.FromBase64String(val.Value);
				default:
					throw new ArgumentException("Unsupported");
			}
		}
	}
}