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
        // The TGS solver (chosen for the rooftop tower) lets small round bits roll and rock on the floor for longer, so pieces
        // shed their spin a little faster and drop off to sleep once they barely move.
        void Awake(){var body=GetComponent<Rigidbody>();if(!body)return;body.angularDamping=Mathf.Max(body.angularDamping,.6f);body.sleepThreshold=Mathf.Max(body.sleepThreshold,.02f);}
    }
}
