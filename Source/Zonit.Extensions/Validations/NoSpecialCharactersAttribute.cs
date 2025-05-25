//using System.ComponentModel.DataAnnotations;
//using System.Text.RegularExpressions;

//namespace Zonit.Core.Attributes.Validations;

//[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
//public partial class NoSpecialCharactersAttribute : ValidationAttribute
//{
//    public override bool IsValid(object? value)
//    {
//        if (value is not string stringValue)
//            return false;

//        if (SearchSpecialCharRegex().IsMatch(stringValue))
//        {
//            ErrorMessage = "Special characters cannot be used.";
//            return false;
//        }

//        return true;
//    }

//    [GeneratedRegex(@"[^\w\s]+")]
//    private static partial Regex SearchSpecialCharRegex();
//}