using UnityEditor;
namespace AfterHours.Editor
{
    // Runs the automated checks from the After Hours menu. Each one needs desktop Play Mode, with no headset runtime active.
    public static class AfterHoursTests
    {
        const string Smoke="After Hours/Tests/Run gameplay journey (Play Mode)",Comfort="After Hours/Tests/Run menu and seated checks (Play Mode)",Navigation="After Hours/Tests/Run navigation checks (Play Mode)",Seat="After Hours/Tests/Run airplane-seat checks (Play Mode)",Mirror="After Hours/Tests/Run mirror and wardrobe checks (Play Mode)",Ocean="After Hours/Tests/Run ocean, clean mode and night checks (Play Mode)",Throw="After Hours/Tests/Run throwing, controllers and Quest 2 checks (Play Mode)";
        [MenuItem(Smoke,false,2000)] static void RunSmoke()=>AfterHoursSmoke.Start();
        [MenuItem(Comfort,false,2001)] static void RunComfort()=>ComfortValidation.Start();
        [MenuItem(Navigation,false,2002)] static void RunNavigation()=>NavigationValidation.Start();
        [MenuItem(Seat,false,2003)] static void RunSeat()=>SeatedReachValidation.Start();
        [MenuItem(Mirror,false,2004)] static void RunMirror()=>WardrobeValidation.Start();
        [MenuItem(Ocean,false,2005)] static void RunOcean()=>OceanValidation.Start();
        [MenuItem(Throw,false,2006)] static void RunThrow()=>ThrowValidation.Start();
        [MenuItem(Smoke,true)][MenuItem(Comfort,true)][MenuItem(Navigation,true)][MenuItem(Seat,true)][MenuItem(Mirror,true)][MenuItem(Ocean,true)][MenuItem(Throw,true)] static bool InPlayMode()=>EditorApplication.isPlaying;
    }
}
