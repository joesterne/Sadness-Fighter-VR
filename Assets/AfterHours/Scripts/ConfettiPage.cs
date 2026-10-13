using UnityEngine;
namespace AfterHours
{
    // Sits on a résumé page and tells the confetti whenever the page hits something. See ConfettiEgg.
    [RequireComponent(typeof(Grabbable))]
    public class ConfettiPage : MonoBehaviour
    {
        public ConfettiEgg egg;
        Grabbable page;
        void OnCollisionEnter(Collision collision)
        {
            if(!egg)return;
            if(!page)page=GetComponent<Grabbable>();
            egg.Hit(page,collision);
        }
    }
}
