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

            Vector3 LRHeading = hand.transform.position - hand.otherHand.transform.position; //vector points from left to right controller
            float LRDistance = LRHeading.magnitude;
            Vector3 LRMiddlePosition = hand.otherHand.transform.position + LRHeading / 2;

            paintLayer = GameObject.Find("Layer 1");

            if (paintLayer)
            {
                if (LGrabWorldClicked & RGrabWorldClicked)
                {

                    paintLayer.transform.position = Vector3.zero;
                    paintLayer.transform.rotation = Quaternion.FromToRotation(LROldHeading, LRHeading) * paintLayerOldRotation;
                    paintLayer.transform.localScale = Vector3.one * (LRDistance - LROldDistance) + paintLayerOldScale;
                    paintLayer.transform.position = (LRMiddlePosition - LROldMiddlePosition) + paintLayerOldPosition;

                }
                else
                {
                    paintLayerOldPosition = paintLayer.transform.position;
                    paintLayerOldScale = paintLayer.transform.lossyScale;
                    paintLayerOldRotation = paintLayer.transform.rotation;
                    LROldDistance = LRDistance;
                    LROldHeading = LRHeading;
                    LROldMiddlePosition = LRMiddlePosition;
                    
                    
                    
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

