using ChocolateyAppMaker.Models.DB;
using ChocolateyAppMaker.Services.Interfaces;
using System.Diagnostics;
using System.Text;

namespace ChocolateyAppMaker.Services.Implementations
{
    public class ChocolateyBuilderService: IChocolateyBuilderService
    {
        private readonly IConfiguration _config;
        private readonly string _outputFolder;

        public ChocolateyBuilderService(IConfiguration config, IWebHostEnvironment env)
        {
            _config = config;
            _outputFolder = Path.Combine(env.ContentRootPath, "GeneratedPackages");
            Directory.CreateDirectory(_outputFolder);
        }

        public async Task<string> BuildPackageAsync(SoftwareProfile profile, InstallerFile file)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"choco_build_{Guid.NewGuid()}");
            var packageId = profile.ChocolateyId ?? profile.Name.ToLower().Replace(" ", "");
            var packageDir = Path.Combine(tempPath, packageId);
            var toolsDir = Path.Combine(packageDir, "tools");

            try
            {
                Directory.CreateDirectory(toolsDir);

                // 1. Копируем установщик
                var destInstaller = Path.Combine(toolsDir, file.Filename);
                File.Copy(file.FilePath, destInstaller, true);

                // 2. Генерируем chocolateyInstall.ps1
                var script = GenerateInstallScript(file, packageId, profile.Name);
                // ВАЖНО: UTF8 + BOM (Preamble) для PowerShell
                await File.WriteAllTextAsync(Path.Combine(toolsDir, "chocolateyInstall.ps1"), script, Encoding.UTF8);

                // 3. Генерируем .nuspec
                var nuspec = GenerateNuspec(profile, file, packageId);
                await File.WriteAllTextAsync(Path.Combine(packageDir, $"{packageId}.nuspec"), nuspec, Encoding.UTF8);

                // 4. Запускаем choco pack
                var chocoPath = _config["ChocoPath"] ?? @"C:\ProgramData\chocolatey\choco.exe";

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = chocoPath,
                        Arguments = $"pack \"{packageId}.nuspec\" --out \"{_outputFolder}\"",
                        WorkingDirectory = packageDir,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    var err = await process.StandardError.ReadToEndAsync();
                    throw new Exception($"Choco pack failed: {err}");
                }

                var nupkgName = $"{packageId}.{file.Version}.nupkg";
                return Path.Combine(_outputFolder, nupkgName);
            }
            finally
            {
                // Очистка временных файлов
                if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
            }
        }

        private string GenerateInstallScript(InstallerFile file, string id, string title)
        {
            // Упрощенная логика аргументов
            string silentArgs = file.Extension == "msi" ? "/quiet /norestart" : "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-";
            string fileType = file.Extension == "msi" ? "msi" : "exe";

            return $@"
$ErrorActionPreference = 'Stop'
$toolsDir   = $(Split-Path -parent $MyInvocation.MyCommand.Definition)
$fileLocation = Join-Path $toolsDir '{file.Filename}'

$packageArgs = @{{
    packageName   = '{id}'
    fileType      = '{fileType}'
    file          = $fileLocation
    softwareName  = '{title}'
    silentArgs    = '{silentArgs}'
    validExitCodes= @(0, 3010, 1641)
}}

Install-ChocolateyInstallPackage @packageArgs
";
        }

        private string GenerateNuspec(SoftwareProfile profile, InstallerFile file, string id)
        {
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2015/06/nuspec.xsd"">
  <metadata>
    <id>{id}</id>
    <version>{file.Version}</version>
    <title>{profile.Name}</title>
    <authors>{profile.Name} Authors</authors>
    <description>{profile.Description ?? profile.Name}</description>
    <tags>{profile.Tags}</tags>
    <iconUrl>{profile.IconUrl ?? "https://community.chocolatey.org/content/images/package-default-icon.png"}</iconUrl>
  </metadata>
  <files>
    <file src=""tools\**"" target=""tools"" />
  </files>
</package>";
        }
    }
}
