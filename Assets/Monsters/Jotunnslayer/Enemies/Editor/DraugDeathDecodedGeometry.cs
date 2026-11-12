using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class DraugDeathDecodedGeometry
{
 const string Folder="Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/DraugThrower/DeathFx/GeometryDecoded";
 [Serializable] public class Sub {public int[] indices;}
 [Serializable] public class Record {public string name;public Vector3[] vertices,normals;public Vector4[] tangents;public Vector2[] uv;public Color[] colors;public Sub[] submeshes;}
 [Serializable] public class Records {public Record[] meshes;}
 static Records Read()=>JsonUtility.FromJson<Records>(File.ReadAllText("Docs/Validation/DraugDeathDecodedMeshes.json"));
 public static Mesh Import(Mesh source)
 {
  Record row=null;foreach(var r in Read().meshes)if(r.name==source.name)row=r;
  if(row==null)return null;
  if(row.vertices.Length!=source.vertexCount)throw new Exception("Decoded source vertex count mismatch");
  Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
  var mesh=new Mesh{name=row.name};mesh.vertices=row.vertices;
  if(row.normals.Length>0)mesh.normals=row.normals;if(row.tangents.Length>0)mesh.tangents=row.tangents;
  if(row.uv.Length>0)mesh.uv=row.uv;if(row.colors.Length>0)mesh.colors=row.colors;
  mesh.subMeshCount=row.submeshes.Length;
  for(int i=0;i<row.submeshes.Length;i++)mesh.SetTriangles(row.submeshes[i].indices,i,false);
  mesh.RecalculateBounds();
  if(Vector3.Distance(mesh.bounds.center,source.bounds.center)>.001f || Vector3.Distance(mesh.bounds.extents,source.bounds.extents)>.001f)throw new Exception("Decoded/source declared bounds mismatch "+row.name);
  string path=Folder+"/"+row.name+".asset";
  var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);
  return mesh;
 }
 static void Equal<T>(T[] expected,T[] actual,string label)
 {
  if(expected.Length!=actual.Length)throw new Exception(label+" length changed");
  for(int i=0;i<expected.Length;i++)if(!expected[i].Equals(actual[i]))throw new Exception(label+" value changed at "+i);
 }
 public static void Validate()
 {
  int vertices=0;
  foreach(var row in Read().meshes)
  {
   var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/"+row.name+".asset");if(!mesh)throw new Exception("Decoded mesh missing "+row.name);
   Equal(row.vertices,mesh.vertices,row.name+" positions");Equal(row.normals,mesh.normals,row.name+" normals");
   Equal(row.tangents,mesh.tangents,row.name+" tangents");Equal(row.uv,mesh.uv,row.name+" UV");Equal(row.colors,mesh.colors,row.name+" colors");
   if(mesh.subMeshCount!=row.submeshes.Length)throw new Exception("Submesh count changed");
   for(int i=0;i<row.submeshes.Length;i++)Equal(row.submeshes[i].indices,mesh.GetTriangles(i),row.name+" indices");
   vertices+=row.vertices.Length;
  }
  if(vertices!=4303)throw new Exception("Expected nine fragments/4303 vertices");
  Debug.Log("DRAUG_DEATH_DECODED_GEOMETRY_PASS meshes=9 vertices=4303 positionsNormalsTangentsUVColorsIndices=True");
 }
}
