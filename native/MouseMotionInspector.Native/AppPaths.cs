namespace MouseMotionInspector.Native;

internal static class AppPaths
{
    internal static string Recordings => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MouseMotionInspector", "recordings");
}
