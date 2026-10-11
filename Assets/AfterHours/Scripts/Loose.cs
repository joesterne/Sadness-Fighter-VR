using System;
using UnityEngine;
namespace AfterHours
{
    // A piece that can break off the computer or a desk thing. Once it has flown, it belongs to the room,
    // and the bat can knock it about. The sound says what it is made of.
    public class Loose : MonoBehaviour
    {
        public BreakSound sound;
        [NonSerialized] public float nextKnock;
    }
}
