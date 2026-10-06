using UnityEditor;
namespace AfterHours.Editor
{
    // Bounded testing aid for an editor that is not advancing game frames while controlled remotely.
    public static class SimulatorTestSession
    {
        static double until,next;
        public static void Begin(int seconds=90)
        {End();until=EditorApplication.timeSinceStartup+seconds;EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup>until){End();return;}
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.017;EditorApplication.Step();
        }
        public static void End(){EditorApplication.update-=Tick;EditorApplication.isPaused=false;}
    }
}
