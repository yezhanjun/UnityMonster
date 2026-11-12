using UnityEngine;
namespace AvatarPortability.PreviewFit
{
    [DisallowMultipleComponent]
    public sealed class PortableStockingCoverage : MonoBehaviour
    {
        public Mesh completeMesh, lowerMesh;
        public bool upperEnabled;
        public void SetUpperEnabled(bool value)
        {
            upperEnabled=value;
            var skin=GetComponentInChildren<SkinnedMeshRenderer>(true);
            Mesh mesh=value?completeMesh:lowerMesh;
            if(!skin || !mesh || skin.sharedMesh==mesh)return;
            skin.sharedMesh=mesh;
            var sync=GetComponent<PortableStockingBodyShapeSync>();
            if(sync){sync.Rebind();sync.Synchronize();}
        }
        void OnEnable(){SetUpperEnabled(upperEnabled);}
        public static void Apply(GameObject root,bool upperEnabled)
        {
            if(!root)return;
            foreach(var coverage in root.GetComponentsInChildren<PortableStockingCoverage>(true))coverage.SetUpperEnabled(upperEnabled);
        }
    }
}
