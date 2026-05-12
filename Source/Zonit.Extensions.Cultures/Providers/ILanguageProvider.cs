namespace Zonit.Extensions.Cultures;

public interface ILanguageProvider
{
    public LanguageModel GetByCode(string culture);
}