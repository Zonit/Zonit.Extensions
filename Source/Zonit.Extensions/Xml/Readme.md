# Zonit.Extensions.Xml

### XmlConvertible:
This XmlConvertible class provides functionality for converting objects to and from XML format. It includes methods for serializing an object into XML and deserializing XML back into an object, utilizing the .NET XML serialization capabilities.

**Model**
```cs
[XmlRoot("response")]
public class UserModel : XmlConvertible
{
    public UserModel() : base() { }
    public UserModel(string xml) : base(xml) {}

    [XmlElement("First")]
    public string FirstName { get; set; } = string.Empty;

    [XmlElement("Last")]
    public string LastName { get; set; } = string.Empty;
}
```

**Serialize**
```cs
var model = new UserModel();
model.FirstName = "Jan";
model.LastName = "Kowalski";
var xml = model.Serialize();
```

**Deserialize**
```cs
var xml = "";
var model = new UserModel(xml);
Console.WriteLine($"{model.FirstName} {model.LastName}");
```

```cs
var xml = "";
var model = XmlConvertible.FromXml<UserModel>(xml);
```