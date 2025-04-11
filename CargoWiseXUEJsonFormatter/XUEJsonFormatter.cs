using CargoWiseXUEJsonFormatter.Models;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CargoWiseXUEJsonFormatter
{
	/// <summary>
	/// Class to convert XUE Json format into actual objects
	/// </summary>
	public class XUEJsonFormatter
	{
		// Regex for finding all occurenced that has a "primary key" value in it
		private readonly Regex _pkRegex;
		// Regex for finding list items
		private readonly Regex _rangeRegex;
		private readonly string _primaryKey;
		private readonly XElement _source;
		private List<ContextNode>? _contextNodes;

		private readonly JsonSerializerSettings _settings = new JsonSerializerSettings
		{
			Error = (sender, args) => { args.ErrorContext.Handled = true; },
			MissingMemberHandling = MissingMemberHandling.Error
		};

		/// <summary>
		/// Instanciate with a "primary" key used to identify entries, as well as what XElement to find the context nodes in.
		/// </summary>
		/// <param name="primaryKey"></param>
		/// <param name="source"></param>
		public XUEJsonFormatter(string primaryKey, XElement source)
		{
			_primaryKey = primaryKey;
			_source = source;
			_pkRegex = new Regex(primaryKey + "\":(.*?),", RegexOptions.Compiled);
			_rangeRegex = new Regex("{(.*?)}", RegexOptions.Compiled);
		}

		/// <summary>
		/// Deserialize context nodes with a specific collection name into a list of actual objects.
		/// If an object is split across two context nodes, they are merged into the list as a single object.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collectionName"></param>
		/// <returns></returns>
		public List<T> Deserialize<T>(string collectionName)
		{
			if (_contextNodes == null)
				_contextNodes = GetContextNodesFromXUE(_source);
			var returnList = new List<T>();
			var collections = _contextNodes.Where(x => x.Name == collectionName).ToList();
			foreach (var node in collections)
				if (JsonConvert.DeserializeObject<T>(node.Value, _settings) is T item)
					returnList.Add(item);
			return returnList;
		}

		/// <summary>
		/// Get a set of context nodes corresponding to a given type name
		/// </summary>
		/// <param name="collectionName"></param>
		/// <returns></returns>
		public List<ContextNode> GetContextNodesOfType(string collectionName)
		{
			if (_contextNodes == null)
				_contextNodes = GetContextNodesFromXUE(_source);
			return _contextNodes.Where(x => x.Name == collectionName).ToList();
		}

		private List<ContextNode> GetContextNodesFromXUE(XElement doc)
		{
			var contexts = doc.Descendants().Where(x => x.Name.LocalName == "ContextCollection").First();
			var targets = new List<ContextNode>();
			foreach (var child in contexts.Elements())
			{
				var typedDecendants = child.Descendants().FirstOrDefault(x => x.Name.LocalName == "Type");
				if (typedDecendants == null)
					continue;
				var type = typedDecendants.Value;
				var valuedDecendants = child.Descendants().FirstOrDefault(x => x.Name.LocalName == "Value");
				if (valuedDecendants == null)
					continue;
				var value = ConvertToActualJson(valuedDecendants.Value);

				if (value.Contains(_primaryKey))
				{
					if (value.StartsWith('{'))
						value = value.Remove(0, 1);
					if (value.EndsWith('}'))
						value = value.Remove(value.Length - 1);

					var range = _rangeRegex.Matches(value);
					for (int i = 0; i < range.Count; i++)
					{
						var match = range[i].Groups[1].Value;
						var id = _pkRegex.Match(match).Groups[1].Value;
						targets.Add(new ContextNode(Regex.Replace(type, @"[\d-]", string.Empty), match, id));
					}
				}
			}

			targets = targets.DistinctBy(x => x.Value).ToList();

			var finalTargets = new List<ContextNode>();
			foreach (var target in targets)
			{
				var any = finalTargets.SingleOrDefault(x => x.ID == target.ID);
				if (any != null)
					any.Value += $", {target.Value}";
				else finalTargets.Add(target);
			}

			foreach (var target in targets)
			{
				target.Value = "{" + target.Value + "}";
			}

			return finalTargets;
		}

		private string ConvertToActualJson(string text)
		{
			text = text.Replace("\"", "\\\"");
			text = text.Replace("|~", "\"");
			return text;
		}
	}
}
