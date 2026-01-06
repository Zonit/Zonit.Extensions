using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Zonit.Extensions.Xml;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public abstract class XmlConvertible
{
    protected XmlConvertible() { }

    protected XmlConvertible(string xml)
    {
        if (string.IsNullOrEmpty(xml))
            throw new ArgumentException("XML string cannot be null or empty.", nameof(xml));

        DeserializeFromXml(xml);
    }

    /// <summary>
    /// Converting the model to XML
    /// </summary>
    /// <returns>XML representation of the object</returns>
    public string Serialize()
    {
        using var stringWriter = new StringWriter();
        using var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings 
        { 
            Indent = true,
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false
        });

        xmlWriter.WriteStartDocument();
        SerializeToXml(xmlWriter);
        xmlWriter.WriteEndDocument();
        
        return stringWriter.ToString();
    }

    /// <summary>
    /// Deserialize XML into this instance
    /// </summary>
    /// <param name="xml">XML string to deserialize</param>
    protected void DeserializeFromXml(string xml)
    {
        try
        {
            using var reader = new StringReader(xml);
            using var xmlReader = XmlReader.Create(reader);
            
            // Move to the root element
            xmlReader.MoveToContent();
            
            DeserializeFromXml(xmlReader);
        }
        catch (Exception ex) when (ex is XmlException)
        {
            throw new XmlSerializationException($"Error deserializing XML: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Create a new instance of T from XML
    /// </summary>
    /// <typeparam name="T">Type that inherits from XmlConvertible</typeparam>
    /// <param name="xml">XML string</param>
    /// <returns>New instance of T</returns>
    public static T FromXml<T>(string xml) where T : XmlConvertible, new()
    {
        if (string.IsNullOrEmpty(xml))
            throw new ArgumentException("XML string cannot be null or empty.", nameof(xml));
            
        var instance = new T();
        instance.DeserializeFromXml(xml);
        return instance;
    }

    /// <summary>
    /// Override this method to implement custom XML serialization
    /// </summary>
    /// <param name="writer">XmlWriter to write to</param>
    protected virtual void SerializeToXml(XmlWriter writer)
    {
        var type = GetType();
        var rootElementName = GetRootElementName(type);
        
        writer.WriteStartElement(rootElementName);
        
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite);
            
        foreach (var prop in properties)
        {
            var value = prop.GetValue(this);
            if (value != null)
            {
                var elementName = GetElementName(prop);
                writer.WriteElementString(elementName, value.ToString());
            }
        }
        
        writer.WriteEndElement();
    }

    /// <summary>
    /// Override this method to implement custom XML deserialization
    /// </summary>
    /// <param name="reader">XmlReader to read from</param>
    protected virtual void DeserializeFromXml(XmlReader reader)
    {
        var type = GetType();
        var rootElementName = GetRootElementName(type);
        
        // Build a dictionary of XML element names to properties
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .ToDictionary(p => GetElementName(p), p => p, StringComparer.OrdinalIgnoreCase);

        // Read the root element
        if (reader.NodeType == XmlNodeType.Element)
        {
            // Skip the root element name check to be more flexible
            reader.Read(); // Move into the element
        }

        while (reader.NodeType != XmlNodeType.EndElement && reader.NodeType != XmlNodeType.None)
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                var elementName = reader.Name;
                
                if (properties.TryGetValue(elementName, out var prop))
                {
                    var value = reader.ReadElementContentAsString();
                    SetPropertyValue(prop, value);
                }
                else
                {
                    reader.Skip();
                }
            }
            else
            {
                reader.Read();
            }
        }
    }

    private static string GetRootElementName([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)] Type type)
    {
        var xmlRootAttr = type.GetCustomAttribute<XmlRootAttribute>();
        return xmlRootAttr?.ElementName ?? type.Name;
    }

    private static string GetElementName(PropertyInfo property)
    {
        var xmlElementAttr = property.GetCustomAttribute<XmlElementAttribute>();
        return xmlElementAttr?.ElementName ?? property.Name;
    }

    private void SetPropertyValue(PropertyInfo property, string value)
    {
        try
        {
            var propertyType = property.PropertyType;
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (string.IsNullOrEmpty(value))
            {
                if (propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) == null)
                    return; // Don't set non-nullable value types to null
                    
                property.SetValue(this, null);
                return;
            }

            object? convertedValue = underlyingType.Name switch
            {
                nameof(String) => value,
                nameof(Int32) => int.Parse(value),
                nameof(Int64) => long.Parse(value),
                nameof(Decimal) => decimal.Parse(value),
                nameof(Double) => double.Parse(value),
                nameof(Single) => float.Parse(value),
                nameof(Boolean) => bool.Parse(value),
                nameof(DateTime) => DateTime.Parse(value),
                nameof(Guid) => Guid.Parse(value),
                _ => Convert.ChangeType(value, underlyingType)
            };

            property.SetValue(this, convertedValue);
        }
        catch (Exception ex)
        {
            throw new XmlSerializationException(
                $"Failed to set property '{property.Name}' with value '{value}': {ex.Message}", ex);
        }
    }
}

public class XmlSerializationException : Exception
{
    public XmlSerializationException(string message) : base(message) { }
    public XmlSerializationException(string message, Exception innerException) : base(message, innerException) { }
}