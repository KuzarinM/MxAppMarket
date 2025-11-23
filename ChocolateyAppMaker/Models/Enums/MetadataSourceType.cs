namespace ChocolateyAppMaker.Models.Enums
{
    /// <summary>
    /// Выбора источника
    /// </summary>
    public enum MetadataSourceType
    {
        /// <summary>
        /// Сначала Flathub, потом Choco + iTunes
        /// </summary>
        Auto,

        /// <summary>
        /// Только Flathub (Linux базы)
        /// </summary>
        Flathub,

        /// <summary>
        /// // Только Chocolatey
        /// </summary>
        Chocolatey
    }
}
