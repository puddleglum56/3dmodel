using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        public Player player;

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
        public float brushSize { get; set; }

        public Vector3 brushScale { get; set; }
        public Vector3 brushPosition { get; set; }
        public Quaternion brushRotation { get; set; }

        public float[,,] space;
        public int resolution; //number of voxels per unity unit
        public Vector3 size;
        public Vector3 center;

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

            FileStream fs = new FileStream(@"d:\tmp\huge_dummy_file", FileMode.CreateNew);
            fs.Seek(2048L, SeekOrigin.Begin);
            fs.WriteByte(1);
            fs.Close();
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
            InitializeVoxelSpace();
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
            UpdateVoxels();
            DrawVoxels();

        }

        private void InitializeVoxelSpace()
        {
            size = new Vector3(10f, 10f, 10f);
            center = hand.transform.position;
            resolution = 100;
            space = new float[(int) (resolution * size[0]), (int) (resolution * size[1]), (int) (resolution * size[2])];
            brushSize = 1f;

        }

        private int[] WtV(Vector3 position)
        {
            float voxelSize = 1 / resolution;
            Vector3 newOrigin = new Vector3(center.x - size.x / 2, center.y - size.y / 2, center.z - size.z / 2);
            Debug.Log(newOrigin);
            int[] voxelPosition = new int[3] { (int) ((position.x - newOrigin.x)/voxelSize), (int) ((position.y - newOrigin.y)/voxelSize), (int) ((position.z - newOrigin.z)/voxelSize)};

            return voxelPosition;
        }

        private void UpdateVoxels()
        {
            int[] brushVoxelPosition = WtV(brushOutlineInstance.transform.position);
            Debug.Log(new Vector3(brushVoxelPosition[0], brushVoxelPosition[1], brushVoxelPosition[2]));
            int brushVoxelSize = (int)brushSize * resolution;
            Debug.Log(brushVoxelSize);

            if (brushNumber == 1)
            {
                int x = brushVoxelSize;
                for (var i = brushVoxelPosition[0] - x ; i < brushVoxelPosition[0] + x; i++)
                {
                    int y = (int)Mathf.Sqrt(Mathf.Pow(brushVoxelSize, 2) - Mathf.Pow((float)i, 2));
                    for (var j = brushVoxelPosition[1] - y; j < brushVoxelPosition[1] + y; j++)
                    {
                        int z = (int)Mathf.Sqrt(Mathf.Pow(brushVoxelSize, 2) - Mathf.Pow((float)i, 2) - Mathf.Pow((float)y, 2));
                        for (var k = brushVoxelPosition[2] - z; j < brushVoxelPosition[2] + z; k++)
                        {
                            space[i, j, k] = 1;
                        }
                    }
                }
            }
        }


        private void DrawVoxels()
        {
            for (var i = 0; i < space.GetLength(0); i++)
            {
                for (var j = 0; j < space.GetLength(1); j++)
                {
                    for (var k = 0; k < space.GetLength(2); k++)
                    {
                        if (space[i, j, k] == 1)
                        {
                            brushSubMeshInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            brushSubMeshInstance.transform.localScale = Vector3.one;
                            brushSubMeshInstance.transform.position = new Vector3(i, j, k);
                        }
                    }

                }

            }

        }
        /*
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
        */

        private void SetBrushSubMeshInstanceTransform()
        {
            brushSubMeshInstance.transform.parent = activePaintLayer.transform;
            brushSubMeshInstance.transform.localScale = brushOutlineInstance.transform.lossyScale;
            brushSubMeshInstance.transform.position = brushOutlineInstance.transform.position;
            brushSubMeshInstance.transform.rotation = brushOutlineInstance.transform.rotation;
        }

        private void CleanUpBrush()
        {

        }

        /*
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
            //AutoWeld(combinedBrushStrokes, 1f, 0.001f);
            brushLayer.GetComponent<MeshFilter>().mesh = combinedBrushStrokes;
            brushLayer.transform.parent = paintLayer.transform;
        }
        */

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

        public static void AutoWeld(Mesh mesh, float threshold, float bucketStep)
        {
            Vector3[] oldVertices = mesh.vertices;
            Vector3[] newVertices = new Vector3[oldVertices.Length];
            int[] old2new = new int[oldVertices.Length];
            int newSize = 0;

            // Find AABB
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < oldVertices.Length; i++)
            {
                if (oldVertices[i].x < min.x) min.x = oldVertices[i].x;
                if (oldVertices[i].y < min.y) min.y = oldVertices[i].y;
                if (oldVertices[i].z < min.z) min.z = oldVertices[i].z;
                if (oldVertices[i].x > max.x) max.x = oldVertices[i].x;
                if (oldVertices[i].y > max.y) max.y = oldVertices[i].y;
                if (oldVertices[i].z > max.z) max.z = oldVertices[i].z;
            }

            // Make cubic buckets, each with dimensions "bucketStep"
            int bucketSizeX = Mathf.FloorToInt((max.x - min.x) / bucketStep) + 1;
            int bucketSizeY = Mathf.FloorToInt((max.y - min.y) / bucketStep) + 1;
            int bucketSizeZ = Mathf.FloorToInt((max.z - min.z) / bucketStep) + 1;
            List<int>[,,] buckets = new List<int>[bucketSizeX, bucketSizeY, bucketSizeZ];

            // Make new vertices
            for (int i = 0; i < oldVertices.Length; i++)
            {
                // Determine which bucket it belongs to
                int x = Mathf.FloorToInt((oldVertices[i].x - min.x) / bucketStep);
                int y = Mathf.FloorToInt((oldVertices[i].y - min.y) / bucketStep);
                int z = Mathf.FloorToInt((oldVertices[i].z - min.z) / bucketStep);

                // Check to see if it's already been added
                if (buckets[x, y, z] == null)
                    buckets[x, y, z] = new List<int>(); // Make buckets lazily

                for (int j = 0; j < buckets[x, y, z].Count; j++)
                {
                    Vector3 to = newVertices[buckets[x, y, z][j]] - oldVertices[i];
                    if (Vector3.SqrMagnitude(to) < threshold)
                    {
                        old2new[i] = buckets[x, y, z][j];
                        goto skip; // Skip to next old vertex if this one is already there
                    }
                }

                // Add new vertex
                newVertices[newSize] = oldVertices[i];
                buckets[x, y, z].Add(newSize);
                old2new[i] = newSize;
                newSize++;

                skip:;
            }

            // Make new triangles
            int[] oldTris = mesh.triangles;
            int[] newTris = new int[oldTris.Length];
            for (int i = 0; i < oldTris.Length; i++)
            {
                newTris[i] = old2new[oldTris[i]];
            }

            Vector3[] finalVertices = new Vector3[newSize];
            for (int i = 0; i < newSize; i++)
                finalVertices[i] = newVertices[i];

            mesh.Clear();
            mesh.vertices = finalVertices;
            mesh.triangles = newTris;
            mesh.RecalculateNormals();
        }

        private void OnSelectActionChange(SteamVR_Action_In actionIn)
        {
        }

    }


}
