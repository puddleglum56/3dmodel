using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Valve.VR.InteractionSystem;
using System.Linq;

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

        bool initialMeshLayed = false;
        Vector3 oldPoint = Vector3.zero;

        public Layer activeLayer;

        public class smartVertex
        {
            public smartVertex()
            {
                triangles = new List<int>();
            }

            public smartVertex(Vector3 position, int type)
            {
                this.position = position;
                this.type = type;
                triangles = new List<int>();
            }

            public Vector3 position { get; set; }
            public int type { get; set; }
            public List<int> triangles { get; set; }
            public Vector3 normal { get; set; }
            public Vector2 uv { get; set; }
            public int order { get; set; }
        }

        public class smartTriangle
        {
            public int[] vertices { get; set; }
            public int type { get; set; }

            public smartTriangle()
            {
            }

            public smartTriangle(int[] vertices)
            {
                this.vertices = vertices;
            }
        }

        public class Stroke
        {
            public List<smartVertex> smartVertices { get; set; }
            public List<smartTriangle> smartTriangles { get; set; }
            public Mesh mesh { get; set; }

            public Stroke()
            {
                smartTriangles = new List<smartTriangle>();
                smartVertices = new List<smartVertex>();
                mesh = new Mesh();
            }

            public Stroke(Mesh mesh)
            {
                smartTriangles = new List<smartTriangle>();
                smartVertices = new List<smartVertex>();
                mesh = new Mesh();

                meshToStroke(mesh);
            }

            public Stroke(GameObject gameObject)
            {
                smartTriangles = new List<smartTriangle>();
                smartVertices = new List<smartVertex>();
                mesh = new Mesh();

                MeshFilter filter = gameObject.GetComponent<MeshFilter>();
                mesh = filter.mesh;
                meshToStroke(mesh);
                for (int v = 0; v < mesh.vertices.Length; v++)
                    smartVertices[v].position = gameObject.transform.TransformPoint(mesh.vertices[v]);
            }

            public void transformToGameObject(GameObject gameObject)
            {
                for (int v = 0; v < smartVertices.Count; v++)
                    smartVertices[v].position = gameObject.transform.TransformPoint(smartVertices[v].position);
            }

            public Mesh strokeToMesh()
            {
                mesh = new Mesh();
                Vector3[] vertices = new Vector3[smartVertices.Count];
                Vector3[] normals = new Vector3[smartVertices.Count];
                Vector2[] uvs = new Vector2[smartVertices.Count];

                int[] triangles = new int[smartTriangles.Count * 3];

                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = smartVertices[i].position;
                    normals[i] = smartVertices[i].normal;
                    uvs[i] = smartVertices[i].uv;
                }
                int tc = 0;
                for (int i = 0; i < triangles.Length; i+=3)
                {
                    triangles[i] = smartTriangles[tc].vertices[0];
                    triangles[i+1] = smartTriangles[tc].vertices[1];
                    triangles[i+2] = smartTriangles[tc].vertices[2];
                    tc++;
                }

                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.uv = uvs;
                mesh.normals = normals;

                return mesh;
            }

            public void meshToStroke(Mesh mesh)
            {
                this.mesh = mesh;
                for (int v = 0; v < mesh.vertices.Length; v++)
                {
                    smartVertex sVert = new smartVertex();
                    sVert.position = mesh.vertices[v];
                    if (v < mesh.normals.Length)
                        sVert.normal = mesh.normals[v];
                    if (v < mesh.uv.Length)
                        sVert.uv = mesh.uv[v];
                    sVert.triangles = new List<int>();
                    sVert.order = v;
                    smartVertices.Add(sVert);
                }
                int tc = 0;
                for (int t = 0; t < mesh.triangles.Length; t+=3)
                {
                    smartTriangle sTri = new smartTriangle();
                    sTri.vertices = new int[] { mesh.triangles[t], mesh.triangles[t + 1], mesh.triangles[t + 2] };
                    sTri.type = 1;
                    smartTriangles.Add(sTri);

                    smartVertices[mesh.triangles[t]].triangles.Add(tc);
                    smartVertices[mesh.triangles[t + 1]].triangles.Add(tc);
                    smartVertices[mesh.triangles[t + 2]].triangles.Add(tc);
                    
                    tc++;
                }
            }
            public void Add(Stroke stroke2)
            {
                List<smartVertex> newVerts = new List<smartVertex>();
                List<smartTriangle> newTris = new List<smartTriangle>();

                for (int v = 0; v < stroke2.smartVertices.Count; v++)
                {
                    smartVertex stroke2Vert = stroke2.smartVertices[v];
                    smartVertex newVert = new smartVertex();
                    newVert.type = stroke2Vert.type;
                    newVert.position = stroke2Vert.position;
                    newVert.normal = stroke2Vert.normal;
                    newVert.uv = stroke2Vert.uv;
                    newVert.triangles = new List<int>();

                    if (stroke2Vert.triangles.Any())
                        for (int vt = 0; vt < stroke2Vert.triangles.Count; vt++)
                        {
                            newVert.triangles.Add(stroke2Vert.triangles[vt] + smartTriangles.Count);
                            newVert.order = stroke2Vert.order + smartVertices.Count;
                        }

                    newVerts.Add(newVert);
                }
                for (int t = 0; t < stroke2.smartTriangles.Count; t++)
                {
                    smartTriangle stroke2Tri = stroke2.smartTriangles[t];
                    smartTriangle newTri = new smartTriangle();
                    newTri.type = stroke2Tri.type;
                    newTri.vertices = new int[stroke2Tri.vertices.Length];

                    for (int tv = 0; tv < 3; tv++)
                        newTri.vertices[tv] = stroke2Tri.vertices[tv] + smartVertices.Count;

                    newTris.Add(newTri);
                }
                smartVertices.AddRange(newVerts);
                smartTriangles.AddRange(newTris);
            }
        }

        public class Layer : List<Stroke> { };

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
            paintLayer.transform.position = Vector3.zero;
            paintLayer.name = "paintLayer";
            activeLayer = new Layer();
            
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
            float brushResolution = 0.1f;

            if (executeBrush & !lastExecuteBrushState)
            {
                oldPoint = hand.transform.position;
            }
            else if (executeBrushState & lastExecuteBrushState)
            {
                if (Vector3.Distance(hand.transform.position, oldPoint) > 0.01f & !initialMeshLayed)
                {
                    StartExtrudeBrush(oldPoint);
                    initialMeshLayed = true;
                }
                if (Vector3.Distance(hand.transform.position, oldPoint) > brushResolution & initialMeshLayed)
                {
                    //was sometimes hitting this if statement twice if oldPoint wasn't immediately changed
                    Vector3 drawPoint = oldPoint;
                    oldPoint = hand.transform.position;
                    MiddleExtrudeBrush(drawPoint);
                }
            }
            else if (!executeBrushState & lastExecuteBrushState)
            {
                initialMeshLayed = false;
                oldPoint = Vector3.zero;
                //EndExtrudeBrush();
            //    CleanUpBrush();
            }
            /*
            if (initialMeshLayed)
                RenderPaint();
            */
        }

        private void RenderPaint()
        {
            Stroke finalStroke = new Stroke();
            Stroke addedStroke = new Stroke();

            for (int s = 0; s < activeLayer.Count; s++)
                finalStroke.Add(activeLayer[s]);

            MeshFilter filter = paintLayer.GetComponent<MeshFilter>();
            Mesh mesh = filter.mesh;
            mesh.Clear();
            Mesh finalMesh = finalStroke.strokeToMesh();

            Color[] colors = new Color[finalStroke.smartVertices.Count];

            for (int i = 0; i < colors.Length; i++)
            {
                if (finalStroke.smartVertices[i].type == 0)
                    colors[i] = Color.blue;
                else
                    colors[i] = Color.black;
            }

            mesh.vertices = finalMesh.vertices;
            mesh.triangles = finalMesh.triangles;
            mesh.uv = finalMesh.uv;
            mesh.normals = finalMesh.normals;
            mesh.colors = colors;
        }

        private void StartExtrudeBrush(Vector3 oldPoint)
        {
            if (brushNumber == 0)
            {
                brushSubMeshInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SetBrushSubMeshInstanceTransform();
            }
            else if (brushNumber == 1)
            {
                brushSubMeshInstance = GameObject.Instantiate<GameObject>(brushSubMeshPrefab);
                brushSubMeshInstance.AddComponent<MeshFilter>();
                Stroke tempStroke = new Stroke();
                brushSubMeshInstance.GetComponent<MeshFilter>().mesh = CreateSphere(tempStroke, true).mesh;
                SetBrushSubMeshInstanceTransform();
                brushSubMeshInstance.transform.rotation = Quaternion.FromToRotation(Vector3.down, hand.transform.position - oldPoint);
                tempStroke.transformToGameObject(brushSubMeshInstance);
                activeLayer.Add(tempStroke);
                RenderPaint();
            }

        }

        private void MiddleExtrudeBrush(Vector3 oldPoint)
        {
            if (brushNumber == 0)
            {
                brushSubMeshInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SetBrushSubMeshInstanceTransform();
            }
            else if (brushNumber == 1)
            {
                brushSubMeshInstance = GameObject.Instantiate<GameObject>(brushSubMeshPrefab);
                brushSubMeshInstance.AddComponent<MeshFilter>();
                Stroke tempStroke = new Stroke();
                brushSubMeshInstance.GetComponent<MeshFilter>().mesh = CreateRing(tempStroke).mesh;
                SetBrushSubMeshInstanceTransform();
                brushSubMeshInstance.transform.rotation = Quaternion.FromToRotation(Vector3.down, hand.transform.position - oldPoint);
                tempStroke.transformToGameObject(brushSubMeshInstance);
                JoinHotVertices(activeLayer.Last(),tempStroke);
                RenderPaint();
            }
        }

        private void DrawDebugSphere(Vector3 position, Color color)
        {
            GameObject debug = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            debug.transform.localScale = 0.05f * Vector3.one;
            debug.transform.position = position;
            debug.GetComponent<Renderer>().material.color = color;
        }

        private void JoinHotVertices(Stroke stroke1, Stroke stroke2)
        {

            var s1Hot = new List<smartVertex>();
            var s2Hot = new List<smartVertex>();

            for (int i = 0; i < stroke1.smartVertices.Count; i++)
                if (stroke1.smartVertices[i].type == 0)
                {
                    s1Hot.Add(stroke1.smartVertices[i]);
                    Debug.Log("im heeerrree");
                    stroke1.smartVertices[i].type = 1;
                }


            for (int i = 0; i < stroke2.smartVertices.Count; i++)
                if (stroke2.smartVertices[i].type == 0)
                {
                    s2Hot.Add(stroke2.smartVertices[i]);
                    DrawDebugSphere(stroke2.smartVertices[i].position,Color.red);
                }


            int nt = 0;

            for (int i = 0; i < s1Hot.Count(); i++)
            {
                float[] dist = new float[s2Hot.Count];
                for (int j = 0; j < s2Hot.Count; j++)
                    dist[j] = Vector3.Distance(s1Hot[i].position, s2Hot[j].position);

                int minDistIndex = FindMinIndex(dist);

                //for now we're assuming we're joining between hemisphere and ring
                int s1current = s1Hot[i].order;
                int s1next = s1current + 1;
                if (s1next > stroke1.smartVertices.Count-1)
                    s1next = 0;
                int s2closest = s2Hot[i].order;
                int s2next = s2closest + 1;
                if (s2next > stroke2.smartVertices.Count-1)
                    s2next = 0;

                //subtract stroke1.smartVertices.Count because it's going to be added again in the stroke.Add() method
                stroke2.smartTriangles.Add(new smartTriangle(new int[] { s1current - stroke1.smartVertices.Count , s2next, s2closest}));
                //stroke2.smartVertices[s2closest].triangles.Add(nt);
                //stroke2.smartVertices[s2next].triangles.Add(nt);
                nt++;
                stroke2.smartTriangles.Add(new smartTriangle(new int[] { s1current - stroke1.smartVertices.Count, s1next - stroke1.smartVertices.Count, s2next }));
                //stroke2.smartVertices[s2next].triangles.Add(nt);
                nt++;
            }

            stroke1.Add(stroke2);
        }

        private int FindMinIndex(float[] arr)
        {
            int pos = 0;
            for (int i=0; i < arr.Length; i++)
                if (arr[i] < arr[pos]) { pos = i; }

            return pos;
        }

        private void EndExtrudeBrush()
        {

        }

        private void SetBrushSubMeshInstanceTransform()
        {
            //brushSubMeshInstance.transform.parent = activePaintLayer.transform;
            //brushSubMeshInstance.transform.localScale = Vector3.one;
            brushSubMeshInstance.transform.position = brushOutlineInstance.transform.position;
            brushSubMeshInstance.transform.rotation = brushOutlineInstance.transform.rotation;
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

        private Stroke CreateSphere(Stroke stroke, bool hemisphere)
        {
            // Longitude |||
            int nbLong = 24;
            // Latitude ---
            int nbLat = 16;
            float radius = 0.25f;

            #region Vertices
            Vector3[] vertices = new Vector3[(nbLong + 1) * ((hemisphere) ? nbLat / 2 : nbLat) + 2];
            float _pi = Mathf.PI;
            float _2pi = _pi * 2f;

            vertices[0] = Vector3.up * radius;
            for (int lat = 0; lat < ((hemisphere) ? nbLat / 2 : nbLat); lat++)
            {
                float a1 = _pi * (float)(lat + 1) / (nbLat + 1);
                float sin1 = Mathf.Sin(a1);
                float cos1 = Mathf.Cos(a1);

                for (int lon = 0; lon <= nbLong; lon++)
                {
                    float a2 = _2pi * (float)(lon == nbLong ? 0 : lon) / nbLong;
                    float sin2 = Mathf.Sin(a2);
                    float cos2 = Mathf.Cos(a2);

                    vertices[lon + lat * (nbLong + 1) + 1] = new Vector3(sin1 * cos2, cos1, sin1 * sin2) * radius;
                }
            }
            vertices[vertices.Length - 1] = Vector3.up * -radius;
            #endregion

            #region Normals		
            Vector3[] normals = new Vector3[vertices.Length];
            for (int n = 0; n < vertices.Length; n++)
                normals[n] = vertices[n].normalized;
            #endregion

            #region UVs
            Vector2[] uvs = new Vector2[vertices.Length];
            uvs[0] = Vector2.up;
            uvs[uvs.Length - 1] = Vector2.zero;
            //for (int lat = 0; lat < nbLat; lat++)
            for (int lat = 0; lat < ((hemisphere) ? nbLat / 2 : nbLat); lat++)
                for (int lon = 0; lon <= nbLong; lon++)
                {
                    //uvs[lon + lat * (nbLong + 1) + 1] = new Vector2((float)lon / nbLong, 1f - (float)(lat + 1) / (nbLat + 1));
                    if (!hemisphere) uvs[lon + lat * (nbLong + 1) + 1] = new Vector2((float)lon / nbLong, 1f - (float)(lat + 1) / (nbLat + 1));
                    else uvs[lon + lat * (nbLong + 1) + 1] = new Vector2((float)lon / nbLong, 1f - (float)(lat + 1) / (nbLat / 2));
                }
            #endregion

            #region Triangles
            int nbFaces = vertices.Length;
            int nbTriangles = nbFaces * 2;
            int nbIndexes = nbTriangles * 3;
            int[] triangles = new int[nbIndexes];

            //Top Cap
            int i = 0;
            for (int lon = 0; lon < nbLong; lon++)
            {
                triangles[i++] = lon + 2;
                triangles[i++] = lon + 1;
                triangles[i++] = 0;
            }

            //Middle
            for (int lat = 0; lat < ((hemisphere) ? nbLat / 2 : nbLat) - 1; lat++)
            {
                for (int lon = 0; lon < nbLong; lon++)
                {
                    int current = lon + lat * (nbLong + 1) + 1;
                    int next = current + nbLong + 1;

                    triangles[i++] = current;
                    triangles[i++] = current + 1;
                    triangles[i++] = next + 1;

                    triangles[i++] = current;
                    triangles[i++] = next + 1;
                    triangles[i++] = next;
                }
            }


            if (!hemisphere)
            {
                //Bottom Cap
                for (int lon = 0; lon < nbLong; lon++)
                {
                    triangles[i++] = vertices.Length - 1;
                    triangles[i++] = vertices.Length - (lon + 2) - 1;
                    triangles[i++] = vertices.Length - (lon + 1) - 1;
                }
            }
            #endregion

            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.normals = normals;

            stroke.meshToStroke(mesh);

            for (int v = 0; v < vertices.Length; v++)
            {
                if (v >= vertices.Length - nbLong - 2)
                    // type 0 is 'hot', meaning we're going to try to join this later
                    stroke.smartVertices[v].type = 0;
                else
                    stroke.smartVertices[v].type = 1;
            }

            return stroke;
        }
        
        private Stroke CreateRing(Stroke stroke)
        {

            // Longitude |||
            int nbLong = 24;
            float radius = 0.25f;

            #region Vertices
            Vector3[] vertices = new Vector3[nbLong];
            float _pi = Mathf.PI;
            float _2pi = _pi * 2f;
            int v = 0;

            for (int lon = 0; lon < nbLong; lon++)
            {
                float a2 = _2pi * (float)lon / nbLong;
                float sin2 = Mathf.Sin(a2);
                float cos2 = Mathf.Cos(a2);

                smartVertex sVert = new smartVertex(new Vector3(cos2, 0, sin2) * radius, 0);
                sVert.type = 0;
                sVert.order = v;
                stroke.smartVertices.Add(sVert);
                v++;
            }
            #endregion

            return stroke;
        }

        private void OnSelectActionChange(SteamVR_Action_In actionIn)
        {
        }

    }


}
