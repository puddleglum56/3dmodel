using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Valve.VR.InteractionSystem;

namespace Valve.VR.InteractionSystem.Sample
{
    public class Brush : MonoBehaviour
    {
        public SteamVR_Action_Vector2 brushMenu; //touching the trackpad opens up the brush menu and changes brush outline
        public SteamVR_Action_Boolean selectBrush; //clicking the trackpad on a brush opens brush options
        public SteamVR_Action_Boolean executeBrush; //clicking the trigger executes the brush (eg. lays down a sphere where the brush outline is)

        public Hand hand;

        public GameObject brushOutlinePrefab; //this is the outline of the brush so you can see where you're going to make an object
        public GameObject brushOutlineInstance;
        public GameObject brushUI; //this is the visible UI that appears on controller model
        public GameObject brushPaintPrefab; //this is the parent of the meshes the user 'paints'
        public GameObject activePaintLayer;
        public GameObject paintLayer;
        public GameObject brushSubMeshPrefab;
        public GameObject brushSubMeshInstance;
        public GameObject brushLayer;

        public int brushNumber { get; set; }
        public Vector3 brushScale { get; set; }
        public Vector3 brushPosition { get; set; }
        public Quaternion brushRotation { get; set; }


        public int layerNumber = 1;

        protected void Awake()
        {
            brushOutlineInstance = GameObject.Instantiate<GameObject>(brushOutlinePrefab);
            brushOutlineInstance.transform.parent = GameObject.Find("RightHand").transform; //hard-coded brush on right hand, can change later
            SetTransformBrushOutline();
            TransformBrushOutline();

            activePaintLayer = GameObject.Instantiate<GameObject>(brushPaintPrefab);
            activePaintLayer.name = "activePaintLayer";
            paintLayer = GameObject.Instantiate<GameObject>(brushPaintPrefab);
            paintLayer.name = "paintLayer";
            brushSubMeshInstance = GameObject.Instantiate<GameObject>(brushSubMeshPrefab);
        }

        private void OnEnable() 
        {
            if (hand == null)
                hand = this.GetComponent<Hand>();

            if (selectBrush == null) //this is just stuff I pretty much copied from Planting.cs, don't think it's necessary bc these should always be bound
            {
                Debug.LogError("No selectBrush action assigned");
                return;
            }

            brushMenu.AddOnChangeListener(OnMenuActionChange, hand.handType);
            selectBrush.AddOnChangeListener(OnSelectActionChange, hand.handType);
            executeBrush.AddOnUpdateListener(OnExecuteActionUpdate, hand.handType);
        }

        private void OnDisable()
        {
            if (selectBrush != null)
                brushMenu.RemoveOnChangeListener(OnMenuActionChange, hand.handType);
                selectBrush.RemoveOnChangeListener(OnMenuActionChange, hand.handType);
                executeBrush.RemoveOnChangeListener(OnMenuActionChange, hand.handType);
        }

        private void SetBrushNumber(Vector2 menuPosition) //this function converts raw trackpad input into a brush number
        {
            float menuPositionAngle;

            menuPositionAngle = Mathf.Atan2(menuPosition.y, menuPosition.x) / Mathf.Deg2Rad; // goes 0-180 then at 180 switches to -180-0
            if (menuPositionAngle < 0)
            {
                menuPositionAngle += 360; //make it 0-360
            }

            float numberOfBrushes = 8f; //how many brushes are we going to support?
            float degreeToSplitBy = 360f / numberOfBrushes;
            brushNumber = (int)Mathf.Floor(menuPositionAngle / degreeToSplitBy); //split trackpad into 8 45-degree slices, get an int 0-7 corresponding to these slices
        }

        private void OnMenuActionChange(SteamVR_Action_In actionIn) //this function reads input from trackpad and calls all the functions to deal with brush switching
        {
            Vector2 menuPosition = brushMenu.GetAxis(hand.handType);
            SetBrushNumber(menuPosition);

            IDictionary<int, string> brushMapping = new Dictionary<int, string>() //this dict isn't used anywhere, just have it here to keep track of brushes
            {
                {0, "cube" },
                {1, "sphere" }
            };

            ChangeBrushOutline(brushNumber);
            RenderBrushUI(); //TODO
        }

        private void OnExecuteActionUpdate(SteamVR_Action_In actionIn) //this function reads input from trigger
        {
            bool executeBrushState = executeBrush.GetState(hand.handType);
            bool lastExecuteBrushState = executeBrush.GetLastState(hand.handType);
            
            if (executeBrushState)
                ExecuteBrush();
            else if (!(executeBrushState) & lastExecuteBrushState)
                CleanUpBrush();
        }

        private void ExecuteBrush()
        {
            if (brushNumber == 0)
            {
                brushSubMeshInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SetBrushSubMeshInstanceTransform();
            }
            else if (brushNumber == 1)
            {
                brushSubMeshInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                SetBrushSubMeshInstanceTransform();
            }
        }

        private void SetBrushSubMeshInstanceTransform()
        {
            brushSubMeshInstance.transform.parent = activePaintLayer.transform;
            brushSubMeshInstance.transform.localScale = brushOutlineInstance.transform.lossyScale;
            brushSubMeshInstance.transform.position = brushOutlineInstance.transform.position;
            brushSubMeshInstance.transform.rotation = brushOutlineInstance.transform.rotation;
        }

        private void CleanUpBrush()
        {

            MeshFilter[] brushStrokes = activePaintLayer.GetComponentsInChildren<MeshFilter>();
            CombineInstance[] combine = new CombineInstance[brushStrokes.Length];
            List<CombineInstance> brushStrokesList = new List<CombineInstance>();

            for (var i = 0; i < brushStrokes.Length; i++)
            {
                if (brushStrokes[i].sharedMesh == null)
                    continue;
                combine[i].mesh = brushStrokes[i].mesh;
                combine[i].transform = brushStrokes[i].transform.localToWorldMatrix;
                brushStrokesList.Add(combine[i]);
            }

            foreach (Transform child in activePaintLayer.transform)
            {
                GameObject.Destroy(child.gameObject);
            }

            Mesh combinedBrushStrokes = new Mesh();
            combinedBrushStrokes.CombineMeshes(brushStrokesList.ToArray());
            brushLayer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            brushLayer.name = "Layer " + layerNumber.ToString(); //TODO layers
            //AutoWeld(combinedBrushStrokes, 0.01f);
            brushLayer.GetComponent<MeshFilter>().mesh = combinedBrushStrokes;
            brushLayer.transform.parent = paintLayer.transform;
        }

        private void ChangeBrushOutline(int brushNumber)
        {
            MeshRenderer[] brushMeshes = brushOutlineInstance.GetComponentsInChildren<MeshRenderer>(true);


            for (var i = 0; i < brushMeshes.Length; i++) //this depends on the child objects in brushOutlinePrefab being in the correct order, maybe a search by name would be better
            {
                MeshRenderer brushMesh = brushMeshes[i];

                if (i == brushNumber)
                    brushMesh.enabled = true;
                else
                    brushMesh.enabled = false;
            }
        }

        private void SetTransformBrushOutline() //these values will be needed to actually paint, so they're public
        {
            brushScale = new Vector3(0.25f, 0.25f, 0.25f);
            brushPosition = new Vector3(0f, 0f, 0.25f);
            brushRotation = Quaternion.identity;

        }
        private void TransformBrushOutline() //actually transform what the brush outline looks like

        {
            brushOutlineInstance.transform.localScale = brushScale;
            brushOutlineInstance.transform.localPosition = brushPosition;
            brushOutlineInstance.transform.localRotation = brushRotation;
        }

        private void RenderBrushUI()
        {
        }

        private void AutoWeld(Mesh mesh, float threshold)
        {
            Vector3[] verts = mesh.vertices;

            // Build new vertex buffer and remove "duplicate" verticies
            // that are within the given threshold.
            List<Vector3> newVerts = new List<Vector3>();
            List<Vector2> newUVs = new List<Vector2>();

            int k = 0;

            foreach (Vector3 vert in verts)
            {
                // Has vertex already been added to newVerts list?
                foreach (Vector3 newVert in newVerts)
                    if (Vector3.Distance(newVert, vert) <= threshold)
                        goto skipToNext;

                // Accept new vertex!
                newVerts.Add(vert);
                newUVs.Add(mesh.uv[k]);

                skipToNext:;
                ++k;
            }

            // Rebuild triangles using new verticies
            int[] tris = mesh.triangles;
            for (int i = 0; i < tris.Length; ++i)
            {
                // Find new vertex point from buffer
                for (int j = 0; j < newVerts.Count; ++j)
                {
                    if (Vector3.Distance(newVerts[j], verts[tris[i]]) <= threshold)
                    {
                        tris[i] = j;
                        break;
                    }
                }
            }

            // Update mesh!
            mesh.Clear();
            mesh.vertices = newVerts.ToArray();
            mesh.triangles = tris;
            mesh.uv = newUVs.ToArray();
            mesh.RecalculateBounds();
        }

        private void OnSelectActionChange(SteamVR_Action_In actionIn)
        {
        }

    }


}
