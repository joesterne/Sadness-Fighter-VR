using UnityEngine;
namespace AfterHours
{
    public class Portal : MonoBehaviour
    {
        public GameDirector director;public int destination;
        void OnTriggerEnter(Collider other){TryEnter(other);}
        // Teleport arrives while the fade owns movement. Retry while occupied once the fade has finished.
        void OnTriggerStay(Collider other){TryEnter(other);}
        void TryEnter(Collider other)
        {var player=other.GetComponentInParent<VRPlayer>();if(player&&player==director.player&&!player.busy)director.Travel(destination);}
    }
}
