using System.Runtime.InteropServices;

namespace Techdinamics.Ship.PrintService.Helpers;

public static class PlatformHelper
{
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
}
