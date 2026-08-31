using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using cchay_optimicer_cs.Models;

namespace cchay_optimicer_cs.Services
{
    public class TweakExportProfile
    {
        public string ProfileName { get; set; } = "Mi Perfil";
        public string Description { get; set; } = "";
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public Dictionary<string, bool> TweakStates { get; set; } = new Dictionary<string, bool>();
        public List<string> DisabledServices { get; set; } = new List<string>();
    }

    public class ProfileService
    {
        public static Task SaveProfileAsync(string filePath, string profileName, string description)
        {
            return Task.Run(async () =>
            {
                var tweaks = await TweakService.GetTweaksAsync();
                var profile = new TweakExportProfile
                {
                    ProfileName = profileName,
                    Description = description,
                    CreatedDate = DateTime.Now
                };

                foreach (var t in tweaks)
                {
                    profile.TweakStates[t.Key] = t.Enabled;
                }

                string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                string? dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                await File.WriteAllTextAsync(filePath, json);
            });
        }

        public static Task<TweakExportProfile?> LoadProfileAsync(string filePath)
        {
            return Task.Run(async () =>
            {
                if (!File.Exists(filePath)) return null;
                string json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<TweakExportProfile>(json);
            });
        }

        public static Task ApplyProfileAsync(TweakExportProfile profile)
        {
            return Task.Run(async () =>
            {
                var tweaks = await TweakService.GetTweaksAsync();
                foreach (var tweak in tweaks)
                {
                    if (profile.TweakStates.TryGetValue(tweak.Key, out bool targetState))
                    {
                        if (targetState)
                        {
                            await TweakService.ApplyTweakAsync(tweak.Key);
                        }
                    }
                }
            });
        }

        public static TweakExportProfile GetGamingPreset()
        {
            var profile = new TweakExportProfile
            {
                ProfileName = "Perfil Gamer Máximo Rendimiento",
                Description = "Desactiva servicios secundarios, habilita prioridad de CPU para juegos y el plan de máxima energía."
            };
            profile.TweakStates["disable-superfetch"] = true;
            profile.TweakStates["disable-indexing"] = true;
            profile.TweakStates["optimize-cpu-priority"] = true;
            profile.TweakStates["disable-power-throttling"] = true;
            profile.TweakStates["disable-background-apps"] = true;
            profile.TweakStates["ultimate-performance"] = true;
            return profile;
        }

        public static TweakExportProfile GetBalancedPreset()
        {
            var profile = new TweakExportProfile
            {
                ProfileName = "Perfil Equilibrado / Diario",
                Description = "Ajustes estándar seguros para uso cotidiano sin sacrificar funciones del sistema."
            };
            profile.TweakStates["disable-superfetch"] = false;
            profile.TweakStates["disable-indexing"] = false;
            profile.TweakStates["optimize-cpu-priority"] = true;
            profile.TweakStates["disable-power-throttling"] = false;
            profile.TweakStates["disable-background-apps"] = false;
            profile.TweakStates["ultimate-performance"] = false;
            return profile;
        }

        public static TweakExportProfile GetDeveloperPreset()
        {
            var profile = new TweakExportProfile
            {
                ProfileName = "Perfil Desarrollador / Estudiante de Ingeniería 👨‍💻",
                Description = "Optimizado para programación (VS Code, Visual Studio, Android Studio). Mantiene la laptop fresca y silenciosa con consumo eficiente."
            };
            profile.TweakStates["disable-superfetch"] = true;
            profile.TweakStates["disable-indexing"] = true;
            profile.TweakStates["optimize-cpu-priority"] = true;
            profile.TweakStates["disable-background-apps"] = true;
            profile.TweakStates["disable-telemetry"] = true;
            profile.TweakStates["disable-power-throttling"] = false; // Mantiene el ahorro eficiente de batería
            profile.TweakStates["ultimate-performance"] = false;     // Evita sobrecalentar la laptop en reposo
            profile.TweakStates["network-latency-tweaks"] = true;    // Red rápida para git clone / npm / pip
            return profile;
        }
    }
}
