namespace CargoWiseXUEJsonFormatter.Models
{
	public class ContextNode
	{
		public string Name { get; set; }
		public string Value { get; set; }
		public string ID { get; set; }

		public ContextNode(string name, string value, string id)
		{
			Name = name;
			Value = value;
			ID = id;
		}
	}
}
