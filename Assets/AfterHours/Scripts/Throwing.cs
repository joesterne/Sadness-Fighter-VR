using UnityEngine;
namespace AfterHours
{
    // How a thrown box (or résumé page) is launched. The player's own throw sets the speed and direction; when it is aimed roughly at the
    // truck (or the shredder), a gentle pull bends it in, so a seated player with a short arm swing still lands it.
    public static class Throwing
    {
        // The hand has to be moving this fast, and sideways enough, for a release to count as a throw. Anything slower is a drop or a point-and-release.
        public const float MinSpeed=1.8f, MinFlat=1f;
        // A real throw is a little stronger than the hand looks, and never faster than a person could manage.
        public const float Boost=1.2f, MaxSpeed=14f;
        // Within this many degrees of the truck the pull is at full strength; it fades away by the second angle.
        public const float FullAssistAngle=25f, NoAssistAngle=55f;
        public const float AssistStrength=.92f;
        // How long a thrown box ignores the player's own body, so a throw from the chest never bounces back.
        public const float IgnoreBody=.35f;
        // A thrown box that has stopped without reaching the truck goes back to its stand after this long.
        public const float RestToReturn=1.6f;

        static Vector3 Flat(Vector3 v){v.y=0;return v;}
        public static bool IsThrow(Vector3 hand)=>hand.magnitude>=MinSpeed&&Flat(hand).magnitude>=MinFlat;
        // The sideways angle between the throw and the direction to a point. A target right beside the player never counts.
        public static float Angle(Vector3 hand,Vector3 from,Vector3 target)
        {
            var toward=Flat(target-from);if(toward.magnitude<.5f||Flat(hand).magnitude<.01f)return 180;
            return Vector3.Angle(Flat(hand),toward);
        }
        // The pull toward the target: nearly the whole throw close to straight at it, fading to nothing at the edge of the cone.
        public static float Strength(float angle)=>AssistStrength*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(FullAssistAngle,NoAssistAngle,angle)));
        public static float FlightTime(float flatDistance)=>Mathf.Clamp(.5f+flatDistance*.075f,.6f,1.5f);
        // The velocity that carries something from one point to another under gravity in a comfortable flight time.
        public static Vector3 Ballistic(Vector3 from,Vector3 to)
        {
            float t=FlightTime(Flat(to-from).magnitude);
            return (to-from-.5f*Physics.gravity*t*t)/t;
        }
        // The launch velocity for a throw, and whether it was bent toward the target. target is null when nothing is in range.
        public static Vector3 Launch(Vector3 hand,Vector3 from,Vector3? target,out bool assisted)
        {
            var v=hand*Boost;if(v.magnitude>MaxSpeed)v=v.normalized*MaxSpeed;
            assisted=false;if(!target.HasValue)return v;
            float pull=Strength(Angle(hand,from,target.Value));if(pull<=.001f)return v;
            var ideal=Ballistic(from,target.Value);if(ideal.magnitude>MaxSpeed)ideal=ideal.normalized*MaxSpeed;
            assisted=true;return Vector3.Lerp(v,ideal,pull);
        }
        // Where a box launched with this velocity from this point would be after a given time, ignoring everything in the way.
        public static Vector3 Position(Vector3 from,Vector3 velocity,float time)=>from+velocity*time+.5f*Physics.gravity*time*time;
    }
}
