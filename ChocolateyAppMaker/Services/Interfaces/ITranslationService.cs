namespace ChocolateyAppMaker.Services.Interfaces
{
    public interface ITranslationService
    {
        Task<string> TranslateToRussianAsync(string text);
    }
}
