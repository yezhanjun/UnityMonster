using System.Collections.Generic;

// Runtime owner shares this registry across FX instances. Read Count before
// selecting LOD; Register only after particle/wreck spawning succeeds. Remove
// on despawn/destroy. Suppressed instances must never be registered.
public sealed class DraugDeathFxRegistry
{
 readonly Dictionary<int,HashSet<int>> groups=new Dictionary<int,HashSet<int>>();

 public int Count(int group)
 {
  return groups.TryGetValue(group,out var instances)?instances.Count:0;
 }

 public void Register(int group,int instanceId)
 {
  if(!groups.TryGetValue(group,out var instances))
  {
   instances=new HashSet<int>();groups.Add(group,instances);
  }
  instances.Add(instanceId);
 }

 public void Remove(int group,int instanceId)
 {
  if(groups.TryGetValue(group,out var instances))instances.Remove(instanceId);
 }
}
