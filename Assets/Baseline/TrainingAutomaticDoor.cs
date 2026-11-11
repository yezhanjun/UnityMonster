using TGGameEngine;
using UnityEngine;

/// <summary>Facility door occupancy uses actors, not arbitrary trigger exits or projectiles.</summary>
[DisallowMultipleComponent]
public sealed class TrainingAutomaticDoor : MonoBehaviour
{
    public Vector3 detectionCenter = new Vector3(0, 1.25f, 0);
    public Vector3 detectionHalfExtents = new Vector3(1.2f, 1.4f, 2.8f);
    public float closeDelay = .8f;
    public float travelSeconds = .55f;
    readonly Collider[] nearby = new Collider[64];
    TGDoor native;
    Vector3[] closed;
    float amount, nextQuery, closeAfter;
    bool occupied;
    int actorMask;
    public bool IsOpen => amount >= .99f;
    public bool IsOccupied => occupied;

    void Awake()
    {
        actorMask = LayerMask.GetMask("character"); // MTPS character controllers use the character layer.
        native = GetComponent<TGDoor>();
        if (!native) { enabled = false; return; }
        native.DoorTriggerMode = false;
        native.enabled = false;
        closed = new Vector3[native.DoorsList.Count];
        for (int i = 0; i < closed.Length; i++)
            closed[i] = native.DoorsList[i].DoorTransform.localPosition;
    }
    void Update()
    {
        if (Time.time >= nextQuery)
        {
            nextQuery = Time.time + .08f;
            int count = Physics.OverlapBoxNonAlloc(transform.TransformPoint(detectionCenter),
                Vector3.Scale(detectionHalfExtents, Abs(transform.lossyScale)), nearby,
                transform.rotation, actorMask, QueryTriggerInteraction.Ignore);
            occupied = count == nearby.Length; // Saturated occupancy must never close on an actor.
            for (int i = 0; i < count; i++)
            {
                var hit = nearby[i];
                if (hit && hit.GetComponentInParent<CharacterController>()) occupied = true;
                nearby[i] = null;
            }
            if (occupied) closeAfter = Time.time + closeDelay;
        }
        amount = Mathf.MoveTowards(amount, occupied || Time.time < closeAfter ? 1f : 0f,
            Time.deltaTime / Mathf.Max(.1f, travelSeconds));
        for (int i = 0; i < closed.Length; i++)
            if (native.DoorsList[i].DoorTransform)
                native.DoorsList[i].DoorTransform.localPosition = closed[i] + native.DoorsList[i].MoveDistance * amount;
    }
    void OnDisable()
    {
        if (closed == null || !native) return;
        for (int i = 0; i < closed.Length; i++)
            if (native.DoorsList[i].DoorTransform) native.DoorsList[i].DoorTransform.localPosition = closed[i];
        amount = 0; occupied = false; closeAfter = nextQuery = 0;
    }
    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
}

