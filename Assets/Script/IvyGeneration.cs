using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

[CreateAssetMenu]
public class IvyGenerationSettings : ScriptableObject
{
    //feedback: added better default values here
    //feedback: now we store all the values we want to be saved in the scriptableObject.
    [Header("Vines")]
    public Material vineMaterial;
    public float segmentLength = 1f;
    public float width = 0.4f;
    public int branchesNumber = 3;
    public int numberOfSegments = 30;
    public float offsetFromSurface = 0.1f;
    public float directionChangeRange = 20f;
    [Header("Leavses")]
    public bool wantLeaf = true;
    public GameObject leafPrefab;
    public Material leafMaterial;
    [Range(0,100)] public float leafProbability = 60f;
    [Range(0, 360)] public float leafRotation = 100f;


}

public class IvyGeneration : EditorWindow
{
    public struct VineSegment
    {
        public Vector3 Position;
        public Vector3 Normal;

        public VineSegment(Vector3 position, Vector3 normal)
        {
            Position = position;
            Normal = normal;
        }
    }
    
    public static bool generationisActive=true;
    public static bool isOpen;
    
    public GameObject ivyParent;
    private List<VineSegment> segments = new();
    private List<int> modelTris = new List<int>{0, 1, 2, 3, 2, 1};
    private IvyGenerationSettings settings;
    
    public Vector3 directionOriginal;
    public Vector3 directionChanged;
    public Vector3 Normal;
    public Vector3 origin;
    public Vector3 originChanged;
    private Vector3 tangent;

    [MenuItem("Tools/"+nameof(IvyGeneration) + " #&v")]
    static void Init()
    {
        if (isOpen)
        {
            generationisActive = !generationisActive;
        }
        GetWindow<IvyGeneration>().Show();
    }
    void OnEnable()
    {
        SceneView.duringSceneGui += OnScene;
        //editor pref are annoying lets just get everything in the scene
        ivyParent = GameObject.Find("Ivy Parent");
        isOpen = true;
        generationisActive = true;
    }

    private void OnDisable()
    {
        //feedback: no need to have a toolIsEnabled boolean if we just unregister the OnScene callback OnDisable
        SceneView.duringSceneGui -= OnScene;
        isOpen = false;
    }

    private Editor settingsEditor;
    private void OnGUI()
    {
        if (settings == null)
        {
            var settingGuids = AssetDatabase.FindAssets("t:" + nameof(IvyGenerationSettings));
            if (settingGuids.Length > 0)
            {
                settings = AssetDatabase.LoadAssetAtPath<IvyGenerationSettings>(AssetDatabase.GUIDToAssetPath(settingGuids[0]));
            }
            else
            {
                settings = ScriptableObject.CreateInstance<IvyGenerationSettings>();
                var assetPath = AssetDatabase.GenerateUniqueAssetPath($"Assets/Default{nameof(IvyGenerationSettings)}.asset");
                AssetDatabase.CreateAsset(this.settings, assetPath);
                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
            }
            settingsEditor = Editor.CreateEditor(settings);
        }
        
        EditorGUI.BeginChangeCheck();
        settings = EditorGUILayout.ObjectField("Settings", settings, typeof(IvyGenerationSettings), false) as IvyGenerationSettings;
        if (EditorGUI.EndChangeCheck() && settings != null)
        {
            settingsEditor = Editor.CreateEditor(settings);
        }
        
        generationisActive = EditorGUILayout.Toggle(new GUIContent("Active Ivy Generation", "Can also use the shortcut shift+alt+v"), generationisActive);
        ivyParent = (GameObject)EditorGUILayout.ObjectField("IvyParent", ivyParent, typeof(GameObject));
        
        EditorGUILayout.Space();
        
        //feedback: this is how we draw the settings' inspector so we don't have to manually recreate the UI
        settingsEditor.OnInspectorGUI();

        if (GUILayout.Button("Clear All Ivy"))
        {
            segments.Clear();
            var branches = GetExistingBranches();
           
            
            Undo.SetCurrentGroupName("Clear All Ivy");
            for (int i = 0; i < branches.Count; i++)
            {
               Undo.DestroyObjectImmediate(branches[i]); 
            }

            if (InternalEditorUtility.tags.Contains("Leaf"))
            {
                var leafs = GetExistingLeafs();
                for (int i = 0; i < leafs.Count; i++)
                {
                    Undo.DestroyObjectImmediate(leafs[i]); 
                }
                
            }
            Undo.IncrementCurrentGroup();
        }
        if (GUILayout.Button("Combine and Clear"))
        {
            segments.Clear();
            BakeVinesIntoSingleMesh();
        }
        if (GUILayout.Button("Apply Material to all"))
        {
            Undo.SetCurrentGroupName("Apply material to all");
            var branches = GetExistingBranches();
            for (int i = 0; i < branches.Count; i++)
            {
                var renderer = branches[i].GetComponent<MeshRenderer>();
                Undo.RecordObject(renderer, "apply material");
                renderer.material = settings.vineMaterial;
            }

            if (settings.leafMaterial != null && InternalEditorUtility.tags.Contains("Leaf"))
            {
                var leafs = GetExistingLeafs();
                for (int i = 0; i < leafs.Count; i++)
                {
                    var renderer = leafs[i].GetComponent<MeshRenderer>();
                    Undo.RecordObject(renderer, "apply material");
                    renderer.material = settings.leafMaterial;
                }
            }
            
            Undo.IncrementCurrentGroup();
        }
        
        
    }
    

    //feedback: we no longer store a list at the start because it may become invalid when the user undos
    List<GameObject> GetExistingBranches()
    {
       return GameObject.FindGameObjectsWithTag("Branch").ToList();
    }

    List<GameObject> GetExistingLeafs()
    {
        return GameObject.FindGameObjectsWithTag("Leaf").ToList();
    }

    void OnScene(SceneView scene)
    {
        Event e = Event.current;
        if (origin != null && originChanged != null)
        {
            Handles.color= Color.red;
            Handles.DrawLine(origin ,origin+directionOriginal);
        
            Handles.color = Color.blue;
            Handles.DrawLine(originChanged, originChanged + directionChanged);

            Handles.color =Color.green;
            Handles.DrawLine(originChanged, originChanged+Normal);
            
            Handles.color = Color.cyan;
            Handles.DrawLine(originChanged, originChanged+tangent);
            
        }

        //feedback: added mouse icon to show where the vine will be spawned
        Vector3 mouse = e.mousePosition;
        Ray initialRayCast = HandleUtility.GUIPointToWorldRay(mouse);
        if (Physics.Raycast(initialRayCast, out var hit))
        {
            Handles.color = Color.green * 0.6f;

            if (!generationisActive)
                Handles.color = new Color(0.7f, 0.7f, 0.7f, 0.6f);

            float discRadius = HandleUtility.GetHandleSize(hit.point) * 0.1f;
            if (generationisActive)
                Handles.DrawSolidDisc(hit.point, hit.normal, discRadius);
            else
                Handles.DrawWireDisc(hit.point, hit.normal, discRadius, 2f);
        }
        
        
        if (e.button == 0 && e.type == EventType.MouseDown && generationisActive)
        {
            Undo.SetCurrentGroupName("Create Branches");
            //add for boucle 
            for (int i = 1; i <= settings.branchesNumber; i++)
            {
                CreateBranch(initialRayCast);
            }
            Undo.IncrementCurrentGroup();
        }
        
        SceneView.currentDrawingSceneView.Repaint();
    }

    void CreateBranch(Ray castPoint)
    {
        segments.Clear();

        int count = 0;
        if (Physics.Raycast(castPoint, out var hit, Mathf.Infinity))
        {
            var vertex = hit.point + (hit.normal * settings.offsetFromSurface);
            segments.Add(new VineSegment(vertex, hit.normal));
            count++;
            Vector3 dir = GetRandomDirection(segments[0].Normal);
            origin = hit.point;
            for (int i = count; i < settings.numberOfSegments; i++)
            {
              // dir = GetRandomDirection(segments.LastOrDefault().Normal, dir, false);
              
               dir = Quaternion.AngleAxis(Random.Range(-settings.directionChangeRange, settings.directionChangeRange), segments.LastOrDefault().Normal) * dir;
              
              dir = dir.normalized;
              Vector3 rayOrigin = default;
                //get the first point with  the random 360 direction
                bool segmentWasAdded = TryAddSegment(segments.LastOrDefault().Position, segments.LastOrDefault().Normal, ref count);
                if (!segmentWasAdded)
                {
                    rayOrigin = segments.LastOrDefault().Position + ( segments.LastOrDefault().Normal * (settings.segmentLength/2));
                    segmentWasAdded = TryAddSegment(rayOrigin, dir, ref count);
                }
                if (!segmentWasAdded)
                {
                    rayOrigin += dir * settings.segmentLength;
                    segmentWasAdded = TryAddSegment(rayOrigin, - segments.LastOrDefault().Normal, ref count);
                }
                if (!segmentWasAdded)
                {
                    rayOrigin += - segments.LastOrDefault().Normal * settings.segmentLength;
                    segmentWasAdded = TryAddSegment(rayOrigin, -dir, ref count);
                }

                directionChanged = dir;
                Normal = segments.LastOrDefault().Normal;
                originChanged = segments.LastOrDefault().Position;
            }
        }
        if (segments.Count > 1)
        {
            CreateGameObject(CreateMesh());  
        }
    }

    private bool TryAddSegment(Vector3 rayOrigin, Vector3 rayDirection, ref int count)
    {
        bool didHit = Physics.Raycast(rayOrigin, rayDirection, out var hitInfo, settings.segmentLength);
        if (didHit)
        {
            var normal = (segments.LastOrDefault().Normal + hitInfo.normal) / 2;
            normal = normal.normalized;
            if (IsLineObstructed(
                    segments.LastOrDefault().Position,
                    hitInfo.point + (hitInfo.normal * settings.offsetFromSurface)))
            {
                
                Vector3 middle = CalculateMiddle(segments.LastOrDefault().Position,
                    hitInfo.point, (normal));
                segments.Add(new VineSegment(middle, normal));
            }

            segments.Add(new VineSegment(
                hitInfo.point + hitInfo.normal * settings.offsetFromSurface,
                hitInfo.normal));
            count++;
        }

        return didHit;
    }

    bool IsLineObstructed(Vector3 from, Vector3 to)
    {
       
        return Physics.Linecast(from, to);
    }

    Vector3 CalculateMiddle(Vector3 p0, Vector3 p1, Vector3 normal)
    {
        Vector3 middle = (p0 + p1) / 2 ;
        //try add a ray cast to get the middle
        if (Physics.Raycast(middle + normal, -normal, out var hit, settings.segmentLength))
        {
            return hit.point + settings.offsetFromSurface * normal;
        }
        else
        {
            var distance = Vector3.Distance(p0, p1)/2f;
            return middle + normal * distance;
        }

    }

    Vector3 GetRandomDirection(Vector3 normal, Vector3 originalDirection = default(Vector3), bool Use360Rotation = true )
    {
        Vector3 tangent;
        Vector3 dir = new Vector3();
        Vector3 tangent1 = Vector3.Cross(normal, Vector3.forward);
        Vector3 tangent2 = Vector3.Cross(normal, Vector3.up);
        if (tangent1.magnitude > tangent2.magnitude)
        { 
            tangent = tangent1;
        }
        else
        {
            tangent = tangent2;
        }

        this.tangent = tangent;

        if (Use360Rotation)
        {
           dir =  Quaternion.AngleAxis(360  + Random.Range(0, 360 ), normal) * tangent;
        }
        else
        {
           //adaot the original direction to the new normal

           // originalDirection = Quaternion.AngleAxis(angle, tangent) * originalDirection;
           directionOriginal = originalDirection;

          //originalDirection = Vector3.RotateTowards(originalDirection, originChanged, 1000000, 10000000);
         //  dir = originalDirection;
           //final dir were i will mix the two
            dir = Quaternion.AngleAxis(Random.Range(-settings.directionChangeRange, settings.directionChangeRange),
             normal) * originalDirection;
        }
        
        dir = dir.normalized;
        return dir;

    }

    GameObject CreateGameObject(Mesh mesh)
    {
        //feedback: replaced for loop with simple "Contains" statement to reduce lines of code (minor change)
        if (!InternalEditorUtility.tags.Contains("Branch"))
        {
            InternalEditorUtility.AddTag("Branch");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        var branches = GetExistingBranches();
        GameObject branch = new GameObject("Branch " + (branches.Count + 1));
        branch.tag = "Branch";
        Undo.RegisterCreatedObjectUndo(branch, "Create Branch");
       
        //feedback: shortened code and added undo
        if (ivyParent == null)
        {
            ivyParent = new GameObject("Ivy Parent");
            Undo.RegisterCreatedObjectUndo(ivyParent, "Create Ivy Parent");
        }
        Undo.SetTransformParent(branch.transform, ivyParent.transform, true, "parent branch to ivy root");
        
        if (settings.vineMaterial == null) {
            settings.vineMaterial = new Material(Shader.Find("Specular"));
        }
        Undo.AddComponent<MeshFilter>(branch);
        Undo.AddComponent<MeshRenderer>(branch);
        branch.GetComponent<MeshRenderer>().material = settings.vineMaterial;
        branch.GetComponent<MeshFilter>().sharedMesh = mesh;
        
        return branch;
    }

    void CreateLeafs(Vector3 point = default(Vector3), Vector3 up = default(Vector3))
    {
        float leafAppear = Random.Range(0, 100);
        if (leafAppear <= settings.leafProbability)
        {
            //  Vector3 forward = new Vector3(Random.Range(0, settings.leafRotation), Random.Range(0,settings.leafRotation), Random.Range(0, settings.leafRotation));
          GameObject  leaf = Instantiate(settings.leafPrefab, point, Quaternion.AngleAxis(Random.Range(-settings.leafRotation, settings.leafRotation), up), ivyParent.transform);
          if (!InternalEditorUtility.tags.Contains("Leaf"))
          {
              InternalEditorUtility.AddTag("Leaf");
              AssetDatabase.SaveAssets();
              AssetDatabase.Refresh();
          }
          leaf.tag = "Leaf";
          if (settings.leafMaterial != null)
          {
              leaf.GetComponent<MeshRenderer>().material = settings.leafMaterial;
          }
        }
    }

    void CreateLeafMerged(Mesh mesh)
    {
       GameObject leaf = new GameObject("Leafs");
        Undo.SetTransformParent(leaf.transform, ivyParent.transform, true, "parent branch to ivy root");
        Undo.AddComponent<MeshFilter>(leaf);
        Undo.AddComponent<MeshRenderer>(leaf);
        leaf.GetComponent<MeshFilter>().sharedMesh = mesh;
        if (!InternalEditorUtility.tags.Contains("Leaf"))
        {
            InternalEditorUtility.AddTag("Leaf");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        leaf.tag = "Leaf";
        if (settings.leafMaterial != null)
        {
            leaf.GetComponent<MeshRenderer>().material = settings.leafMaterial;
        }
    }
    
    

    Mesh CreateMesh()
    {
        Mesh branchMesh = new Mesh();
        Vector3[] vertices = new Vector3[(segments.Count) * 2];
        Vector2[] uv = new Vector2[segments.Count * 2];
        int[] triangles = new int[(segments.Count - 1 ) * 6];//multiply by 6 because 2 triangle of 3 vertice each by node


        for (int i = 0; i < segments.Count; i++)
        {
            var forward = Vector3.zero;
            if (i > 0)
            {
                forward = segments[i - 1].Position - segments[i].Position;
            }
            if (i < segments.Count - 1)
            {
                forward += segments[i].Position - segments[i + 1].Position;
            }

            if (forward == Vector3.zero)
            {
                forward = Vector3.forward;
            }

            var up = segments[i].Normal;
            up.Normalize();
            forward.Normalize();

            for (int v = 0; v < 2; v++)//create the vertice (2 per node)
            {
                Vector3 pos = segments[i].Position;
                
                //get the width for the branch
                //feedback: good use of the cross product!
                Vector3 widthAxis = Vector3.Cross(forward, up);

                if (v == 0 )
                {
                    pos = pos + (settings.width / 2) * widthAxis;
                   vertices[i*2+0] = pos ;

                }
                else if (v ==1)
                {
                    pos = pos - (settings.width / 2) * widthAxis;
                    vertices[i*2+v] = pos;
                }
                if (settings.wantLeaf && settings.leafPrefab != null)
                {
                    CreateLeafs(vertices[i*2+v], up);
                }
                
            }
            
            //create uv 
            for (int j = 0; j < 2; j++)
            {
                //detect if i is even or odd to see if it is a top or bottom uv
                if (i % 2 == 0)
                {
                    uv[i * 2 + j] = new Vector2(j, 0);
                }
                else
                {
                    uv[i * 2 + j] = new Vector2(j, 1);
                }
            }
           
            
            //create triangles
            if ((i + 1) < segments.Count)
            {
                for (int v = 0; v < modelTris.Count; v++)
                {
                    triangles[(i * 6) + v] = modelTris[v]+ (2 * i);
                }
            }
        }

        branchMesh.vertices = vertices;
        branchMesh.triangles = triangles;
        branchMesh.uv = uv;
        branchMesh.RecalculateNormals();
        
        return branchMesh;

    }

    void BakeVinesIntoSingleMesh()
    {
        Undo.SetCurrentGroupName(nameof(BakeVinesIntoSingleMesh));
        var branches = GetExistingBranches();
        var leafs = GetExistingLeafs();
        //combine everything and get a new game object for this
        Mesh finalBranch = new Mesh();
        Mesh finalLeaf = new Mesh();
        CombineInstance[] combineLeaf = new CombineInstance[leafs.Count];
        CombineInstance[] combine = new CombineInstance[branches.Count];

        for (int i = 0; i < branches.Count; i++)
        {
            combine[i].mesh = branches[i].GetComponent<MeshFilter>().sharedMesh;
            combine[i].transform = branches[i].transform.localToWorldMatrix;
        }
        for(int i = 0; i < leafs.Count; i++)
        {
            combineLeaf[i].mesh = leafs[i].GetComponent<MeshFilter>().sharedMesh;
            combineLeaf[i].transform = leafs[i].transform.localToWorldMatrix;
        }
        finalLeaf.CombineMeshes(combineLeaf);

        finalBranch.CombineMeshes(combine);

        foreach (GameObject branch in branches)
        {
            Undo.DestroyObjectImmediate(branch);
        }

        foreach (GameObject leaf in leafs)
        {
            Undo.DestroyObjectImmediate(leaf);
        }

        CreateLeafMerged( finalLeaf);

        Selection.activeGameObject = CreateGameObject(finalBranch);
        Undo.IncrementCurrentGroup();
    }
}