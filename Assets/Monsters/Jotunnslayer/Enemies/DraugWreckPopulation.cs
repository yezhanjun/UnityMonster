// One shared source-Wreck population per owning runtime. This is distinct from
// the FX LOD group count. Registration counts the entire body array, even when
// optimization disables individual fragments. The lifecycle owner retains its
// registration flag and must unregister before replacing its body array.
public sealed class DraugWreckPopulation
{
 public int ActiveBodies {get;private set;}
 public int ActivationCounter;

 public void Register(int bodyCount,ref bool registered)
 {
  if(registered)return;
  ActiveBodies=unchecked(ActiveBodies+bodyCount);
  registered=true;
 }

 public void Unregister(int bodyCount,ref bool registered)
 {
  if(!registered)return;
  ActiveBodies=unchecked(ActiveBodies-bodyCount);
  registered=false;
 }
}
