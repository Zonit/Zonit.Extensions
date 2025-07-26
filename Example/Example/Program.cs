using Microsoft.AspNetCore.Components;
using Zonit.Extensions.Text;
using Zonit.Extensions.Website;
namespace Example;

internal class Program
{
    static void Main(string[] args)
    {
        var translated = new Translated("Hello, {0}!");

        Test1(translated.AsMarkup()); // MarkupString
        Test2(translated.ToString()); // Explicit conversion do string
        Test3(translated);

    }

    public static void Test1(MarkupString translated)
    {
        var test = translated;
    }

    public static void Test2(string translated)
    {
        var test = translated;
    }

    public static void Test3(Translated translated)
    {
        var test = translated;
    }
}
