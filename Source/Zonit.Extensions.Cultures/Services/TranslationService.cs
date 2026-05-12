using Zonit.Extensions.Cultures.Models;
using Zonit.Extensions.Cultures.Repositories;

namespace Zonit.Extensions.Cultures.Services;

internal class TranslationService(TranslationRepository translationRepository) : ITranslationManager
{
    public void Add(Variable item)
        => translationRepository.Add(item);

    public void AddRange(List<Variable> items)
        => translationRepository.AddRange(items);
}