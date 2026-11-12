using System.Collections.Generic;
using UnityEngine;

namespace Jotunnslayer
{
    internal sealed class MagicMissileTrails : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static event System.Action<uint, bool, bool> ValidationReceived;
#endif
        private sealed class Flight
        {
            internal TrailRenderer trail;
            internal Vector3 position, destination;
            internal Queue<Vector3> waypoints;
            internal float expires;
            internal bool finished;
        }

        private readonly Dictionary<uint, Flight> flights = new Dictionary<uint, Flight>(24);
        private readonly List<uint> expired = new List<uint>(24);
        private Material material, arrowMaterial;

        internal void Receive(uint id, Vector3 position, bool finished, bool heartArrow)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ValidationReceived?.Invoke(id, finished, heartArrow);
#endif
            if (finished)
            {
                if (flights.TryGetValue(id, out var ending))
                {
                    SetDestination(ending, position);
                    ending.finished = true;
                }
                return;
            }

            if (!flights.TryGetValue(id, out var flight))
            {
                Material trailMaterial = heartArrow ? arrowMaterial : material;
                if (!trailMaterial)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (!shader) shader = Shader.Find("Sprites/Default");
                    if (!shader) return;
                    trailMaterial = new Material(shader)
                    {
                        color = heartArrow ? new Color(1f, .69f, .28f) : new Color(.69f, .43f, 1f)
                    };
                    if (heartArrow) arrowMaterial = trailMaterial;
                    else material = trailMaterial;
                }
                var trail = new GameObject(heartArrow ? "Raid Heart Seeker trail" : "Raid Magic Missile trail")
                    .AddComponent<TrailRenderer>();
                trail.transform.SetParent(transform, true);
                trail.transform.position = position;
                trail.sharedMaterial = trailMaterial;
                trail.time = .18f;
                trail.startWidth = heartArrow ? .08f : .13f;
                trail.endWidth = .02f;
                trail.Clear();
                flight = new Flight
                {
                    trail = trail,
                    position = position,
                    destination = position,
                    waypoints = heartArrow ? new Queue<Vector3>(8) : null
                };
                flights.Add(id, flight);
            }

            SetDestination(flight, position);
            flight.expires = Time.unscaledTime + .6f;
        }

        private static void SetDestination(Flight flight, Vector3 destination)
        {
            if (flight.waypoints != null && (flight.position != flight.destination || flight.waypoints.Count > 0))
                flight.waypoints.Enqueue(destination);
            else
                flight.destination = destination;
        }

        private void LateUpdate()
        {
            foreach (var pair in flights)
            {
                var flight = pair.Value;
                if (!flight.trail)
                {
                    expired.Add(pair.Key);
                    continue;
                }
                if (Time.unscaledTime >= flight.expires)
                {
                    End(flight);
                    expired.Add(pair.Key);
                    continue;
                }

                float travel = 17f * Time.deltaTime;
                while (flight.waypoints != null && flight.waypoints.Count > 0)
                {
                    float segment = Vector3.Distance(flight.position, flight.destination);
                    if (segment > travel)
                        break;
                    flight.position = flight.destination;
                    flight.trail.AddPosition(flight.position);
                    travel -= segment;
                    flight.destination = flight.waypoints.Dequeue();
                }
                flight.position = Vector3.MoveTowards(flight.position, flight.destination, travel);
                flight.trail.transform.position = flight.position;
                if (flight.finished && flight.position == flight.destination &&
                    (flight.waypoints == null || flight.waypoints.Count == 0))
                {
                    End(flight);
                    expired.Add(pair.Key);
                }
            }

            foreach (uint id in expired)
                flights.Remove(id);
            expired.Clear();
        }

        private void End(Flight flight)
        {
            flight.trail.emitting = false;
            Destroy(flight.trail.gameObject, flight.trail.time);
        }

        private void OnDestroy()
        {
            foreach (var flight in flights.Values)
                if (flight.trail)
                    Destroy(flight.trail.gameObject);
            if (material)
                Destroy(material);
            if (arrowMaterial)
                Destroy(arrowMaterial);
        }
    }
}
