using System;
using UnityEngine;
namespace AfterHours
{
    // One web in The NDA web. Its trigger volume is what the machete cuts through (pointer rays and teleports pass through triggers);
    // the silk is one merged, see-through mesh that grows from the web's hub. See WebRoom for how it plays.
    public class Cobweb : MonoBehaviour
    {
        public WebRoom room;
        public int index;
        public Collider volume;
        public Transform silk;
        // Cut and waiting to grow back, when it was cut, and how far it has grown back (1 is whole).
        [NonSerialized] public bool cut;
        [NonSerialized] public float cutAt, grown=1;
    }
}
