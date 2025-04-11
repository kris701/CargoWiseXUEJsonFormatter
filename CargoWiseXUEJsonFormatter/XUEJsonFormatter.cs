using CargoWiseXUEJsonFormatter.Models;
using Newtonsoft.Json;
using System.Runtime;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CargoWiseXUEJsonFormatter
{
    public class XUEJsonFormatter
    {
		// Regex for finding all occurenced that has a "primary key" value in it
		private Regex _pkRegex;
		// Regex for finding list items
		private Regex _rangeRegex;
		private string _primaryKey;
		private XElement _source;
		private List<ContextNode>? _contextNodes;

		private readonly JsonSerializerSettings _settings = new JsonSerializerSettings
		{
			Error = (sender, args) => { args.ErrorContext.Handled = true; },
			MissingMemberHandling = MissingMemberHandling.Error
		};

		public XUEJsonFormatter(string primaryKey, XElement source)
		{
			_primaryKey = primaryKey;
			_pkRegex = new Regex(primaryKey + "\":(.*?),", RegexOptions.Compiled);
			_rangeRegex = new Regex("{(.*?)}", RegexOptions.Compiled);
		}

		public List<T> GetNodesOfType<T>(string collectionName)
		{
			if (_contextNodes == null)
				_contextNodes = GetContextNodesFromXUE(_source);
			var returnList = new List<T>();
			var collections = _contextNodes.Where(x => x.Name == collectionName).ToList();
			foreach(var node in collections)
				if (JsonConvert.DeserializeObject<T>(node.Value, _settings) is T item)
					returnList.Add(item);
			return returnList;
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
