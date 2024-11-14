using System;
using System.IO;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using CUE4Parse.UE4.Assets.Exports;
using System.Diagnostics;
using CUE4Parse_Conversion.Sounds;


namespace ExportUtilityWwise
{
    public class AudioExportConfig
    {
        public string gameDirectory { get; set; }
        public string aesKey { get; set; }
        public string objectPath { get; set; }
        public EGame gameOverride { get; set; }
    }
    public static class EventBasedAudioExportVal
    {
        private static AudioExportConfig _config;

        private const string ConfigFileName = "AudioExportConfig.json";
        private const string DefaultGameDirectory = @"C:/UntilDawn/UntilDawn/Windows/Bates/Content/Paks";
        private const string DefaultAesKey = "0x0000000000000000000000000000000000000000000000000000000000000000";
        private const string DefaultObjectPath = "Bates/Content/WwiseAudio/Events/MUS/MUS_Events_WU_Prologue/Play_MUS_BAT_Prologue.uasset";
        private const EGame DefaultGameOverride = EGame.GAME_UE5_3;



        public static void Main(string[] args)
        {
            LoadConfig();
            var provider = new DefaultFileProvider(_config.gameDirectory, SearchOption.TopDirectoryOnly, true, new VersionContainer(_config.gameOverride));
            provider.Initialize();
            provider.SubmitKey(new FGuid(), new FAesKey(_config.aesKey));
            provider.LoadLocalization(ELanguage.English);

            var allExports = provider.LoadObjectExports(_config.objectPath);
            var mediaExportFolder = Path.Combine("MediaExports");

            if (!Directory.Exists(mediaExportFolder))
            {
                Directory.CreateDirectory(mediaExportFolder);
            }

            foreach (var export in allExports)
            {
                if (export.Class.Name == "AkAudioEventData") { SaveAudio(export, mediaExportFolder); }
            }
            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();

        }

        private static void LoadConfig()
        {
            if (File.Exists(ConfigFileName))
            {
                Console.WriteLine("Reading config file {0}", ConfigFileName);
                var configJson = File.ReadAllText(ConfigFileName);
                _config = JsonConvert.DeserializeObject<AudioExportConfig>(configJson);
            }
            else
            {
                Console.WriteLine("Creating config file {0}", ConfigFileName);
                _config = new AudioExportConfig
                {
                    gameDirectory = DefaultGameDirectory,
                    aesKey = DefaultAesKey,
                    objectPath = DefaultObjectPath,
                    gameOverride = DefaultGameOverride
                };
                SaveConfig();
            }
        }
        private static void SaveConfig()
        {
            var jsonSerializerSettings = new JsonSerializerSettings();
            jsonSerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
            var configJson = JsonConvert.SerializeObject(_config, Formatting.Indented , jsonSerializerSettings);
            File.WriteAllText(ConfigFileName, configJson);
        }
        private static void SaveAudio(UObject export, string mediaExportFolder)
        {
            var eventName = export.Outer.Name;
            var game_path = export.Owner.Name;
            var mediaList = export.GetOrDefault<UObject[]>("MediaList");
            for (int i = 0; i < mediaList.Length; i++)
            {
                var media = mediaList[i];
                var assetData = media.GetOrDefault<UObject>("CurrentMediaAssetData");
                assetData.Decode(false, out var audioFormat, out var data);

                if (data == null || string.IsNullOrEmpty(audioFormat) || assetData.Owner == null)
                {
                    Console.WriteLine($"Error: Could not decode audio for {eventName}_{i}");
                    continue;
                }

                var mediaFileName = i == 0 ? $"{eventName}.wem" : $"{eventName}_{i}.wem";

                var mediaFilePath = Path.Combine(mediaExportFolder, game_path) + ".wem";

                if (!Directory.Exists(Path.GetDirectoryName(mediaFilePath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(mediaFilePath));
                }
                using var stream = new FileStream(mediaFilePath, FileMode.Create, FileAccess.Write);
                using var writer = new BinaryWriter(stream);
                writer.Write(data);
                Console.WriteLine($"Saved {mediaFileName} at {mediaExportFolder}!");

                var wavFilePath = mediaFilePath.Replace(".wem", ".wav");
                ConvertAudioToWav(mediaFilePath, wavFilePath);
            }
        }

        private static void ConvertAudioToWav(string inputFilePath, string outputFilePath)
        {
            var vgmstreamPath = Path.Combine("vgmstream-win", "test.exe");
            if (vgmstreamPath == null)
            {
                Console.WriteLine("Could not find VgmStream installed, Please install from here https://github.com/vgmstream/vgmstream/releases/latest/download/vgmstream-win.zip on your AudioExport folder. ");
                return;
            }
            var vgmProcess = Process.Start(new ProcessStartInfo
            {
                FileName = vgmstreamPath,
                Arguments = $"-o \"{outputFilePath}\" \"{inputFilePath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });

            vgmProcess?.WaitForExit();
            Console.WriteLine("Audio conversion finished.");
            vgmProcess?.Dispose();
        }
    }
    }
