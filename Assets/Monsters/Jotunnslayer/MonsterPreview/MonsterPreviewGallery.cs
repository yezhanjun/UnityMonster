using System;
using UnityEngine;

namespace Jotunnslayer.Preview
{
 [CreateAssetMenu(menuName="Jotunnslayer/Monster preview gallery")]
 public sealed class MonsterPreviewGallery:ScriptableObject
 {
  [Serializable] public sealed class Entry
  {
   public string id,folder;
   public GameObject model;
   public Avatar avatar;
   public AnimationClip[] clips;
   public GameObject[] effects;
  }
  public Entry[] entries;
 }
}
