using UnityEngine;
namespace AfterHours
{
    // Turns about its own vertical axis. A positive speed turns clockwise seen from above (Unity's positive Y rotation).
    // The lighthouse beam uses it: one slow turn takes about nine seconds.
    public class Spin : MonoBehaviour
    {
        public float degreesPerSecond=40f;
        void Update(){transform.Rotate(0,degreesPerSecond*Time.deltaTime,0,Space.Self);}
    }
}
