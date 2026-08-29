using System.Runtime;
using System.Text;
using Stelliberty.Application.Diagnostics;
using Stelliberty.Desktop.Services;
using Stelliberty.Infrastructure.Diagnostics;

namespace Stelliberty.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 低延迟 GC 减少 UI 卡顿，适用于低内存/低核心硬件。
        GCSettings.LatencyMode = GCLatencyMode.LowLatency;
        Console.OutputEncoding = Encoding.UTF8;
        AppLogger.Configure(new CapturedAppLogger(DesktopApplicationLayout.RunningLogFilePath));
        DependencyDirectoryService.Configure();
        AppRuntime.Run(args);
    }
}
