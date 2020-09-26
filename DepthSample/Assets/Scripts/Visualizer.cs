using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Visualizer : MonoBehaviour
{
    Mesh mesh;
    int[] indices;
    // Start is called before the first frame update
  
    public void UpdateMeshInfo(Vector3[] vertices, Color[] colors)
    {
        if (mesh == null)
        {

            mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            //PointCloudの点の数はDepthのピクセル数から計算
            int num = vertices.Length;
            indices = new int[num];
            for (int i = 0; i < num; i++) { indices[i] = i; }

            //meshを初期化
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.SetIndices(indices, MeshTopology.Points, 0);

            //meshを登場させる
            gameObject.GetComponent<MeshFilter>().mesh = mesh;
        }
        else
        {
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }
    }
}
