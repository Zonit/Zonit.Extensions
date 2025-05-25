//using System.ComponentModel.DataAnnotations;

//namespace Zonit.Core.Attributes.Validations;

//[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
//public class WordsCountAttribute(int _minWords, int _maxWords) : ValidationAttribute
//{
//    public override bool IsValid(object? value)
//    {
//        if (value is not string stringValue)
//            return false;

//        string[] words = stringValue.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
//        int wordCount = words.Length;
//        if (wordCount < _minWords || wordCount > _maxWords)
//        {
//            ErrorMessage = $"Name must contain between {_minWords} and {_maxWords} words.";
//            return false;
//        }

//        return true;
//    }
//}