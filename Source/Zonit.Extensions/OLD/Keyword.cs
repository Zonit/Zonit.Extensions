//namespace Zonit.Extensions.Text;

///// <summary>
///// Model reprezentujący słowo kluczowe i jego analizę.
///// </summary>
//public class Keyword
//{
//    /// <summary>
//    /// Treść słowa kluczowego.
//    /// </summary>
//    public string Word { get; }

//    /// <summary>
//    /// Liczba wystąpień słowa kluczowego w tekście.
//    /// </summary>
//    public int Count { get; set; }

//    /// <summary>
//    /// Gęstość słowa kluczowego (procent wystąpień w stosunku do całkowitej liczby słów).
//    /// </summary>
//    public double Density { get; set; }

//    /// <summary>
//    /// Pozycje w tekście, w których występuje słowo kluczowe.
//    /// </summary>
//    public List<int> Positions { get; } = new List<int>();

//    /// <summary>
//    /// Inicjalizuje nowe słowo kluczowe.
//    /// </summary>
//    public Keyword(string word)
//    {
//        Word = word;
//    }
//}