using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace cchay_optimicer_cs.Services
{
    public class QuickOptimizeService
    {
        public static async Task RunQuickOptimizeAsync(Action<string, int> progressCallback)
        {
            var keys = new HashSet<string> { "temp", "windowsTemp", "thumbnails", "errorReports" };
            var targets = DiskService.GetTargets().Where(t => keys.Contains(t.Key)).ToList();
            double freed = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                progressCallback($"Limpiando {targets[i].Name}…", i * 100 / targets.Count);
                var result = await DiskService.ProcessTargetAsync(targets[i], delete: true);
                if (!result.Cleaned)
                    throw new InvalidOperationException($"No se completó la limpieza de {targets[i].Name}.");
                freed += result.SizeMB;
            }
            SettingsService.Settings.TotalMbDiskFreed += freed;
            SettingsService.SaveSettings();
            progressCallback($"Mantenimiento terminado: {freed:F1} MB eliminados. Los archivos en uso se conservan.", 100);
        }
    }
}
