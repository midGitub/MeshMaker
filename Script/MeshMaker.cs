﻿using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

#region 前缀
[ExecuteInEditMode, DisallowMultipleComponent, AddComponentMenu("模型网格编辑器/MeshMaker")]
#endregion
public class MeshMaker : MonoBehaviour 
{
    //最大处理顶点数
    private int _astrictVertexNum = 10000;
    //物体网格
    private Mesh _mesh;
    //目标物体
    private GameObject _target;
    public GameObject Target
    {
        get{
            return _target;
        }
    }
    //克隆体名称
    private string _name = "复制体";
    //所有顶点
    private List<Vector3> _allVertexs;
    //所有三角面
    private List<int> _allTriangles;
    //所有UV
    private List<Vector2> _allUVs;
    //所有法线
    private List<Vector3> _allNormals;

    //顶点
    public List<MeshMakerVertex> Vertexs;
    //三角面
    public List<MeshMakerTriangle> Triangles;
    //选中目标的颜色
    public Color CheckedColor;
    //经过目标的颜色
    public Color HoverColor;
    //点编辑模式控制柄的大小
    public float VertexHandleSize;

    public MeshFilter _meshFilter;
    public MeshRenderer _meshRenderer;
    public MeshCollider _meshCollider;

    private void Awake()
    {
        Init();
    }
    private void OnDestroy()
    {
        this.CompileOnlyEditor(delegate ()
        {
            if (_target)
                DestroyImmediate(_target);
        });
    }

    /// <summary>
    /// 初始化
    /// </summary>
    private void Init()
    {
        #region 检测Mesh
        this.CompileNoEditor(delegate ()
        {
            Destroy(this);
            return;
        });

        if (Application.isPlaying)
        {
            DestroyImmediate(this);
            return;
        }

        string msseage = "OK";
        if (GetComponent<MeshFilter>() == null)
        {
            msseage = "游戏物体缺少组件 MeshFilter！";
        }
        else if (GetComponent<MeshRenderer>() == null)
        {
            msseage = "游戏物体缺少组件 MeshRenderer！";
        }
        else if (GetComponent<MeshFilter>().sharedMesh.vertexCount > _astrictVertexNum)
        {
            msseage = "游戏物体顶点太多，我无法处理！";
        }
        
        if (msseage != "OK")
        {
            DestroyImmediate(this);
            Debug.LogWarning(msseage);
            return;
        }
        #endregion

        #region 识别MeshVertex
        _allUVs = new List<Vector2>(GetComponent<MeshFilter>().sharedMesh.uv);
        _allNormals = new List<Vector3>(GetComponent<MeshFilter>().sharedMesh.normals);
        _allVertexs = new List<Vector3>(GetComponent<MeshFilter>().sharedMesh.vertices);

        Vertexs = new List<MeshMakerVertex>();

        List<int> repetitionVertices = new List<int>();
        for (int i = 0; i < _allVertexs.Count; i++)
        {
            this.CompileOnlyEditor(delegate ()
            {
                EditorUtility.DisplayProgressBar("识别顶点", "正在识别顶点（" + i + "/" + _allVertexs.Count + "）......", 1.0f / _allVertexs.Count * i);
            });
            
            if (repetitionVertices.Contains(i))
                continue;

            List<int> verticesGroup = new List<int>();
            verticesGroup.Add(i);
            
            for (int j = i + 1; j < _allVertexs.Count; j++)
            {
                if (_allVertexs[i] == _allVertexs[j])
                {
                    verticesGroup.Add(j);
                    repetitionVertices.Add(j);
                }
            }
            Vertexs.Add(new MeshMakerVertex(Vertexs.Count, transform.localToWorldMatrix.MultiplyPoint3x4(_allVertexs[i]), _allUVs[i], _allNormals[i], verticesGroup));
        }
        #endregion

        #region 识别MeshTriangle
        _allTriangles = new List<int>(GetComponent<MeshFilter>().sharedMesh.triangles);

        Triangles = new List<MeshMakerTriangle>();
        
        for (int i = 0; (i + 2) < _allTriangles.Count; i += 3)
        {
            this.CompileOnlyEditor(delegate ()
            {
                EditorUtility.DisplayProgressBar("识别顶点", "正在识别顶点（" + i + "/" + _allTriangles.Count + "）......", 1.0f / _allTriangles.Count * i);
            });

            MeshMakerVertex mmv1 = Vertexs.GetVertexByIndex(_allTriangles[i]);
            MeshMakerVertex mmv2 = Vertexs.GetVertexByIndex(_allTriangles[i + 1]);
            MeshMakerVertex mmv3 = Vertexs.GetVertexByIndex(_allTriangles[i + 2]);
            MeshMakerTriangle mmt = new MeshMakerTriangle(Triangles.Count, mmv1, mmv2, mmv3);
            Triangles.Add(mmt);
        }
        for (int i = 0; i < Vertexs.Count; i++)
        {
            Vertexs[i].VertexIndexs.Clear();
        }
        #endregion

        #region 重构Mesh
        Transform tf = transform.FindChild(transform.name + "(Clone)");
        if (tf != null) DestroyImmediate(tf.gameObject);

        _target = new GameObject(transform.name + "(Clone)");
        _target.transform.SetParent(transform);
        _target.transform.localPosition = Vector3.zero;
        _target.transform.localRotation = Quaternion.Euler(Vector3.zero);
        _target.transform.localScale = Vector3.one;
        _target.hideFlags = HideFlags.HideInHierarchy;
        _meshFilter = _target.AddComponent<MeshFilter>();
        _meshRenderer = _target.AddComponent<MeshRenderer>();
        _meshRenderer.sharedMaterial = GetComponent<MeshRenderer>().sharedMaterial;
        _meshCollider = _target.AddComponent<MeshCollider>();

        GenerateMesh();
        RefreshMesh();

        GetComponent<MeshRenderer>().enabled = false;
        if (GetComponent<Collider>())
            GetComponent<Collider>().enabled = false;
        EditorUtility.ClearProgressBar();
        #endregion

        #region 其他编辑器参数初始化
        CheckedColor = Color.red;
        HoverColor = Color.green;
        VertexHandleSize = 0.005f;
        #endregion
    }

    /// <summary>
    /// 重新生成网格（改变网格组成结构时，比如删除顶点，新增顶点）
    /// </summary>
    public void GenerateMesh()
    {
        _allVertexs = new List<Vector3>();
        _allUVs = new List<Vector2>();
        _allNormals = new List<Vector3>();
        _allTriangles = new List<int>();

        for (int i = 0; i < Vertexs.Count; i++)
        {
            Vector3 vertex = _target.transform.worldToLocalMatrix.MultiplyPoint3x4(Vertexs[i].Vertex);
            _allVertexs.Add(vertex);
            _allUVs.Add(Vertexs[i].UV);
            _allNormals.Add(Vertexs[i].Normal);
        }
        for (int i = 0; i < Triangles.Count; i++)
        {
            _allTriangles.Add(Triangles[i].Vertex1.ID);
            _allTriangles.Add(Triangles[i].Vertex2.ID);
            _allTriangles.Add(Triangles[i].Vertex3.ID);
        }
    }

    /// <summary>
    /// 刷新网格（只刷新网格顶点的位置）
    /// </summary>
    public void RefreshMesh()
    {
        this.CompileOnlyEditor(delegate ()
        {
            Undo.RecordObject(this, "Edit Mesh");
        });

        if (!_mesh)
        {
            _mesh = new Mesh();
            _mesh.name = _name + transform.name;
        }

        for (int i = 0; i < Vertexs.Count; i++)
        {
            Vector3 vertex = _target.transform.worldToLocalMatrix.MultiplyPoint3x4(Vertexs[i].Vertex);
            _allVertexs[i] = vertex;
        }

        _mesh.Clear();
        _mesh.vertices = _allVertexs.ToArray();
        _mesh.triangles = _allTriangles.ToArray();
        _mesh.uv = _allUVs.ToArray();
        _mesh.normals = _allNormals.ToArray();
        _meshFilter.sharedMesh = _mesh;
        _meshCollider.sharedMesh = _mesh;
        _mesh.RecalculateNormals();
    }
}
