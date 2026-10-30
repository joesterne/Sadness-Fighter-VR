using UnityEngine;
using TMPro;
namespace AfterHours
{
    // A tower of wooden blocks on a little table on the rooftop, played with real physics: ease a loose block out of the tower and lay it
    // on top, and try not to bring the whole thing down. Tracked hands pinch a block to hold it and can nudge one with a fingertip;
    // controllers squeeze to hold, and the tip of the controller nudges. Nothing here is scored by the game: the sign only counts what is
    // on top, and remembers the best tower.
    //
    // What makes it feel real:
    //   - Blocks are twice the size of a real set (3 x 5 x 15 cm, 140 g) so hands and controllers can work them, with the same proportions,
    //     wood-on-wood friction and no bounce.
    //   - Like a real set, blocks are not all quite the same height. In about two rows in three the middle block is up to 2.5% short and
    //     carries nothing, and the rest are tight. Feeling for the loose ones is the game.
    //   - A held block is not stuck to the hand. It is pulled toward it with about the force of a pinch, so a block with the tower's
    //     weight on it will not come, and the hand slips off if it goes too far. A fingertip is a small ball pushed the same way.
    //   - While the player is on the roof, physics runs at 90 steps a second instead of 50, every block solves its contacts 24 times a step,
    //     contacts start a millimetre and a half out, and the tower is put to sleep the moment it is stacked, so it stands still until touched.
    // Lives on its own object in the rooftop room, so it is switched on and off with the room.
    public class BlockTower : MonoBehaviour
    {
        public const int Levels=18,PerLevel=3;
        public const float Length=.15f,Width=.05f,Height=.03f,Mass=.14f;
        // A loose block is between 40% of this and all of it shorter than full height (0.3 to 0.75 mm), so it is clearly free.
        public const float HeightSpread=.025f;
        public const float StaticFriction=.42f,DynamicFriction=.3f;
        public const int SolverIterations=24,VelocityIterations=8;
        public const float PhysicsRate=90,ContactOffset=.0015f;
        // A fingertip pushes with at most this force (newtons), and gives up and slips through if it is left this far behind.
        public const float PushForce=6,PushSlip=.07f,TipRadius=.011f;
        // The player's best tower, kept across launches.
        public const string BestKey="AfterHours.TowerBest";
        // The tower in the room the player is in, or null.
        public static BlockTower Playing{get;private set;}

        public GameDirector director;
        public TowerBlock[] blocks=new TowerBlock[0];
        // The centre of the tower's foot, on the table top.
        public Transform foot;
        public Collider tableTop;
        public TextMeshPro score;
        public AudioClip knock,tumble;
        // The seed for the next tower's heights. Each new tower takes the next one.
        public int seed=20261028;

        public int OnTop{get;private set;}
        public int Best{get;private set;}
        public bool Fallen{get;private set;}
        public int Towers{get;private set;}
        // The rows' rest heights above the foot (bottom of each row), for the newest tower.
        public float[] RowBase{get;private set;}=new float[Levels+1];
        public float TopHeight=>RowBase[Levels];
        // For the tests: a fingertip position to use instead of a tracked hand's, per hand (null: use the hand).
        [System.NonSerialized] public Vector3?[] fingertipOverride=new Vector3?[2];

        PhysicsMaterial wood,felt;
        bool ready;float savedRate;bool rateChanged;
        Rigidbody[] tips=new Rigidbody[2];bool[] tipLive=new bool[2];
        float nextKnock,nextResist,nextTouchPulse;

        void Awake(){Prepare();}
        public void Prepare()
        {
            if(ready)return;ready=true;
            if(!director)director=GameDirector.Instance;
            wood=new PhysicsMaterial("Tower wood"){staticFriction=StaticFriction,dynamicFriction=DynamicFriction,bounciness=0,frictionCombine=PhysicsMaterialCombine.Average,bounceCombine=PhysicsMaterialCombine.Minimum};
            // The table has a felt top: a little grippier than the wood, and just as dead.
            felt=new PhysicsMaterial("Tower table felt"){staticFriction=.6f,dynamicFriction=.5f,bounciness=0,frictionCombine=PhysicsMaterialCombine.Average,bounceCombine=PhysicsMaterialCombine.Minimum};
            if(tableTop){tableTop.sharedMaterial=felt;tableTop.contactOffset=ContactOffset;}
            for(int i=0;i<blocks.Length;i++)
            {
                var b=blocks[i];b.tower=this;b.index=i;b.Prepare();
                var body=b.body;body.mass=Mass;body.useGravity=true;body.linearDamping=.02f;body.angularDamping=.12f;
                body.solverIterations=SolverIterations;body.solverVelocityIterations=VelocityIterations;
                // Asleep below about 4 cm a second; pushed apart gently when overlapping, never flung.
                body.sleepThreshold=.0008f;body.maxDepenetrationVelocity=.25f;body.maxAngularVelocity=20;
                body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.Discrete;
                foreach(var c in b.GetComponentsInChildren<Collider>()){c.sharedMaterial=wood;c.contactOffset=ContactOffset;}
            }
            for(int i=0;i<2;i++)tips[i]=MakeTip(i);
            Best=PlayerPrefs.GetInt(BestKey,0);
            NewTower(false);
        }
        Rigidbody MakeTip(int hand)
        {
            var go=new GameObject(hand==0?"Left fingertip":"Right fingertip");go.transform.SetParent(transform,false);go.layer=2;
            var sphere=go.AddComponent<SphereCollider>();sphere.radius=TipRadius;sphere.contactOffset=ContactOffset;sphere.sharedMaterial=felt;sphere.enabled=false;
            var body=go.AddComponent<Rigidbody>();body.mass=.3f;body.useGravity=false;body.linearDamping=0;body.constraints=RigidbodyConstraints.FreezeRotation;
            body.isKinematic=true;body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            body.solverIterations=SolverIterations;body.solverVelocityIterations=VelocityIterations;
            return body;
        }
        void OnEnable()
        {
            Playing=this;
            if(Application.isPlaying){savedRate=Time.fixedDeltaTime;Time.fixedDeltaTime=1f/PhysicsRate;rateChanged=true;}
            // A tower left lying in a heap is stacked again for the next visit.
            if(ready&&Fallen)NewTower(false);
            IgnorePlayer();
        }
        void OnDisable()
        {
            if(Playing==this)Playing=null;
            if(rateChanged){Time.fixedDeltaTime=savedRate;rateChanged=false;}
            for(int i=0;i<2;i++)Retire(i);
        }
        void IgnorePlayer()
        {
            var d=director?director:GameDirector.Instance;if(!d||!d.player)return;var body=d.player.GetComponent<CharacterController>();if(!body)return;
            foreach(var t in tips)if(t)Physics.IgnoreCollision(t.GetComponent<Collider>(),body,true);
        }

        // Stacks a fresh tower: the blocks shuffled into new places, every block still and asleep. Each row is one of two kinds: the middle
        // block loose (two rows in three) or all three tight. Tight blocks are exactly full height, so the rows above always rest squarely
        // on both edge blocks. (A loose edge block would leave the row above balanced on the middle block's edge, and the tower leans.)
        public void NewTower(bool announce=true)
        {
            var d=director?director:GameDirector.Instance;
            if(d&&d.player)foreach(var b in blocks)if(b.grab.held)d.player.Drop(b.grab);
            // A live fingertip would be inside the new tower. It comes back once the finger is clear of the blocks.
            for(int i=0;i<2;i++)Retire(i);
            var random=new System.Random(seed++);
            var order=new int[blocks.Length];for(int i=0;i<order.Length;i++)order[i]=i;
            for(int i=order.Length-1;i>0;i--){int j=random.Next(i+1);(order[i],order[j])=(order[j],order[i]);}
            float y=0;
            for(int level=0;level<Levels;level++)
            {
                RowBase[level]=y;
                int loose=random.NextDouble()<.65?1:-1;
                for(int k=0;k<PerLevel;k++)
                {
                    var b=blocks[order[level*PerLevel+k]];
                    float h=k==loose?Height*(1-HeightSpread*(.4f+.6f*(float)random.NextDouble())):Height;
                    // Even rows lie along x, odd rows along z. Each block sits a hair apart from its neighbour so none start out touching sideways.
                    bool alongX=level%2==0;float across=(k-1)*(Width+.0006f);
                    var local=alongX?new Vector3(0,y+h/2,across):new Vector3(across,y+h/2,0);
                    b.Stack(level,h,foot.TransformPoint(local),foot.rotation*Quaternion.Euler(0,alongX?0:90,0));
                }
                y+=Height;
            }
            RowBase[Levels]=y;
            foreach(var b in blocks)b.Settle();
            Fallen=false;OnTop=0;Towers++;
            Refresh();
            if(announce&&d){d.PlayAt(d.place,foot.position+Vector3.up*.3f,.5f);d.Say("A new tower. Look for a loose block: a gentle push tells you.");}
        }

        void FixedUpdate()
        {
            var d=director?director:GameDirector.Instance;if(!d||!d.player)return;
            for(int i=0;i<2;i++)
            {
                Vector3 tip;bool have;
                if(fingertipOverride[i].HasValue){tip=fingertipOverride[i].Value;have=true;}else have=d.player.TryFingertip(i,out tip);
                // Only near the tower: a fingertip pushes nothing else on the roof.
                if(have)have=Near(tip);
                if(!have){Retire(i);continue;}
                Push(i,tip);
            }
        }
        bool Near(Vector3 point)
        {
            var local=foot.InverseTransformPoint(point);
            return new Vector2(local.x,local.z).magnitude<.45f&&local.y>-.1f&&local.y<TopHeight+.35f;
        }
        void Push(int hand,Vector3 tip)
        {
            var body=tips[hand];var sphere=body.GetComponent<SphereCollider>();
            if(!tipLive[hand])
            {
                // A fingertip comes alive only where it is not already inside a block, so it never shoves one out from within.
                if(Physics.CheckSphere(tip,TipRadius,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))return;
                body.isKinematic=true;body.position=tip;body.transform.position=tip;body.isKinematic=false;body.linearVelocity=Vector3.zero;
                sphere.enabled=true;tipLive[hand]=true;return;
            }
            var error=tip-body.position;
            // Left behind (the finger went through a block that would not move): it lets go, and comes back once the finger is clear.
            if(error.magnitude>PushSlip){Retire(hand);return;}
            float dt=Time.fixedDeltaTime;
            var wanted=Vector3.ClampMagnitude(error/dt*.6f,2.5f);
            body.AddForce(Vector3.ClampMagnitude(body.mass*(wanted-body.linearVelocity)/dt,PushForce));
        }
        void Retire(int hand)
        {
            if(!tips[hand]||!tipLive[hand])return;
            tipLive[hand]=false;var body=tips[hand];
            if(!body.isKinematic){body.linearVelocity=Vector3.zero;}
            body.isKinematic=true;body.GetComponent<SphereCollider>().enabled=false;
        }
        public bool TipLive(int hand)=>tipLive[hand];
        public Vector3 TipPosition(int hand)=>tips[hand].position;

        void Update()
        {
            var d=director?director:GameDirector.Instance;
            int moved=0,top=0;
            foreach(var b in blocks)
            {
                if(b.grab.held)b.touched=true;
                if(!b.touched&&b.Displaced)moved++;
                if(b.touched&&!b.grab.held&&b.OnTopOf(this))top++;
            }
            // Two blocks nobody touched have dropped or tipped: the tower is coming down.
            if(!Fallen&&moved>=2)Fall();
            if(!Fallen&&top!=OnTop){OnTop=top;if(OnTop>Best){Best=OnTop;PlayerPrefs.SetInt(BestKey,Best);PlayerPrefs.Save();}Refresh();}
            // A held block that is being held back by the tower pulls at the hand: soft taps, firmer the harder it resists.
            if(d&&d.player&&Time.time>nextResist)foreach(var b in blocks)
            {
                if(!b.grab.held||b.grab.HoldError<.012f)continue;
                d.player.PulseFor(b.grab,.15f+.5f*Mathf.InverseLerp(.012f,.06f,b.grab.HoldError),VRPlayer.HapticPattern.Tap);nextResist=Time.time+.12f;
            }
        }
        void Fall()
        {
            Fallen=true;var d=director?director:GameDirector.Instance;
            if(OnTop>Best){Best=OnTop;PlayerPrefs.SetInt(BestKey,Best);PlayerPrefs.Save();}
            Refresh();
            if(!d)return;
            d.PlayAt(tumble,foot.position+Vector3.up*TopHeight*.5f,.8f);
            if(d.player)for(int i=0;i<2;i++)if(tipLive[i]||HoldingHere(d.player,i))d.player.PulseHand(i,VRPlayer.HapticPattern.Smash,.7f);
            d.Say(OnTop>0?"Down it comes. "+OnTop+(OnTop==1?" block":" blocks")+" on top. That is part of the game. NEW TOWER stacks it again.":"Down it comes. That is part of the game. NEW TOWER stacks it again.");
        }
        bool HoldingHere(VRPlayer player,int hand){foreach(var b in blocks)if(b.grab.held&&player.HoldingIn(hand)==b.grab)return true;return false;}
        void Refresh()
        {
            if(score)score.text=(Fallen?"IT FELL.  ":"ON TOP  "+OnTop+"     ")+"BEST  "+Best;
        }

        // A block knocks against something: a wooden tock, louder the harder it hits, and never more than about twenty a second.
        public void Knock(TowerBlock block,Collision collision)
        {
            float speed=collision.relativeVelocity.magnitude;
            var d=director?director:GameDirector.Instance;if(!d)return;
            int tip=collision.rigidbody&&collision.rigidbody==tips[0]?0:collision.rigidbody&&collision.rigidbody==tips[1]?1:-1;
            if(tip>=0){block.touched=true;if(d.player&&Time.time>nextTouchPulse){d.player.PulseHand(tip,VRPlayer.HapticPattern.Tap,Mathf.Clamp(.2f+speed,.2f,.6f));nextTouchPulse=Time.time+.15f;}}
            if(speed<.12f||Time.time<nextKnock)return;nextKnock=Time.time+.05f;
            d.PlayAt(knock,collision.contactCount>0?collision.GetContact(0).point:block.transform.position,Mathf.Clamp01(speed/1.2f)*.6f+.05f);
            // A held block that bumps something is felt in that hand.
            if(block.grab.held&&d.player)d.player.PulseFor(block.grab,Mathf.Clamp(speed*.6f,.15f,.8f),VRPlayer.HapticPattern.Hit);
        }
        // A block that fell off the roof comes back to rest on the table, beside the tower on one of its four sides, on top of any block already there.
        // There are more places than blocks, so one is always free even with every block off the tower.
        public void Recover(TowerBlock block)
        {
            var sides=new[]{new Vector3(.25f,0,0),new Vector3(-.25f,0,0),new Vector3(0,0,.25f),new Vector3(0,0,-.25f)};
            Vector3 position=default;Quaternion rotation=default;
            for(int layer=0;layer<=Levels*PerLevel/sides.Length;layer++)foreach(var side in sides)
            {
                // Lying lengthwise along the tower's side, so it never reaches into the tower or off the table.
                rotation=foot.rotation*Quaternion.Euler(0,side.x!=0?90:0,0);
                position=foot.TransformPoint(side+Vector3.up*(layer*Height+block.height*.5f+.001f));
                var half=new Vector3(Length,block.height,Width)*.47f;
                if(!Physics.CheckBox(position,half,rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)){block.Lay(position,rotation);return;}
            }
            block.Lay(position,rotation);
        }
        // The block in the tower closest to a point, if one is within reach of it.
        public TowerBlock Nearest(Vector3 point,float reach)
        {
            TowerBlock best=null;float closest=reach;
            foreach(var b in blocks)
            {
                if(b.grab.held)continue;
                float distance=Vector3.Distance(point,b.box.ClosestPoint(point));
                if(distance<closest){closest=distance;best=b;}
            }
            return best;
        }
    }
}
