using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Zonit.Extensions.Xml;

public class XmlConvertible
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
            Encoding = Encoding.UTF8
        });

        var serializer = new XmlSerializer(GetType());
        var ns = new XmlSerializerNamespaces();
        ns.Add(string.Empty, string.Empty); // Remove default namespaces
        
        serializer.Serialize(xmlWriter, this, ns);
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
            var serializer = new XmlSerializer(GetType());
            using var reader = new StringReader(xml);

            if (serializer.Deserialize(reader) is not XmlConvertible deserialized)
                throw new InvalidOperationException("Failed to deserialize XML into model.");

            CopyProperties(deserialized);
        }
        catch (Exception ex) when (
            ex is InvalidOperationException || 
            ex is InvalidCastException || 
            ex is XmlException)
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
            
        try
        {
            var serializer = new XmlSerializer(typeof(T));
            using var reader = new StringReader(xml);
            
            return serializer.Deserialize(reader) as T 
                ?? throw new InvalidOperationException("Failed to deserialize XML into model.");
        }
        catch (Exception ex) when (
            ex is InvalidOperationException || 
            ex is InvalidCastException || 
            ex is XmlException)
        {
            throw new XmlSerializationException($"Error deserializing XML: {ex.Message}", ex);
        }
    }

    private void CopyProperties(XmlConvertible source)
    {
        var properties = GetType().GetProperties()
            .Where(p => p.CanWrite && p.CanRead);
            
        foreach (var prop in properties)
        {
            var sourceValue = prop.GetValue(source);
            prop.SetValue(this, sourceValue);
        }
    }
}

public class XmlSerializationException : Exception
{
    public XmlSerializationException(string message) : base(message) { }
    public XmlSerializationException(string message, Exception innerException) : base(message, innerException) { }
}