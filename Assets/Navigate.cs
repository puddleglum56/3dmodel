using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Valve.VR.InteractionSystem;

namespace Valve.VR.InteractionSystem.Sample
{
    public class Navigate : MonoBehaviour
    {

        public SteamVR_Action_Boolean grabWorld;
        public Hand hand;
        public float velocityTimeOffset = -0.011f;
        public GameObject paintLayer;
        public Vector3 paintLayerOldScale = new Vector3();
        public Vector3 paintLayerOldPosition = new Vector3();
        public float LROldDistance = new float();
        public Vector3 LROldHeading = new Vector3();
        public Quaternion paintLayerOldRotation = new Quaternion();
        public Vector3 LROldMiddlePosition = new Vector3();
        public GameObject transformLayer;

        protected void Awake()
        {
            transformLayer = new GameObject();
            transformLayer.name = "transformLayer";
        }

        private void OnEnable() 
        {
            if (hand == null)
                hand = this.GetComponent<Hand>();

            if (grabWorld == null) //this is just stuff I pretty much copied from Planting.cs, don't think it's necessary bc these should always be bound
            {
                Debug.LogError("No LgrabWorld action assigned");
                return;
            }
            grabWorld.AddOnUpdateListener(OnGrabWorldUpdate, hand.otherHand.handType);
            grabWorld.AddOnUpdateListener(OnGrabWorldUpdate, hand.handType);
        }

        private void OnGrabWorldUpdate(SteamVR_Action_In actionIn)
        {
            bool LGrabWorldClicked = grabWorld.GetState(hand.otherHand.handType);
            bool RGrabWorldClicked = grabWorld.GetState(hand.handType);
            bool lastLGrabWorldClicked = grabWorld.GetLastState(hand.otherHand.handType);
            bool lastRGrabWorldClicked = grabWorld.GetLastState(hand.handType);

            Vector3 LRHeading = hand.transform.position - hand.otherHand.transform.position; //vector points from left to right controller
            float LRDistance = LRHeading.magnitude;
            Vector3 LRMiddlePosition = hand.otherHand.transform.position + LRHeading / 2;

            paintLayer = GameObject.Find("paintLayer");

            if (paintLayer)
            {
                if (LGrabWorldClicked & RGrabWorldClicked)
                {
                    transformLayer.transform.position = LRMiddlePosition;
                    if (!lastLGrabWorldClicked | !lastRGrabWorldClicked)
                        paintLayer.transform.parent = transformLayer.transform;
                    transformLayer.transform.rotation = Quaternion.FromToRotation(LROldHeading, LRHeading) * paintLayerOldRotation;
                    transformLayer.transform.localScale = Vector3.one * (LRDistance - LROldDistance) + paintLayerOldScale;
                }
                else
                {
                    paintLayer.transform.parent = null;
                    paintLayerOldPosition = transformLayer.transform.position;
                    paintLayerOldScale = transformLayer.transform.lossyScale;
                    paintLayerOldRotation = transformLayer.transform.rotation;
                    LROldDistance = LRDistance;
                    LROldHeading = LRHeading;
                }
            }
        }

        private void OnDisable()
        {
            if (grabWorld != null)
            {
                grabWorld.RemoveOnChangeListener(OnGrabWorldUpdate, hand.otherHand.handType);
            }
        }

    }
}

