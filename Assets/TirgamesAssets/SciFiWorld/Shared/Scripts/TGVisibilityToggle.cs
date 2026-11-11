using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TGGameEngine
{
    public class TGVisibilityToggle : MonoBehaviour
    {
        [Tooltip("How long (in seconds) each object stays visible")]
        public float showDuration = 2f;

        [Tooltip("Start cycling visibility on Start")]
        public bool startOnPlay = true;

        [SerializeField]
        private List<GameObject> childObjects = new List<GameObject>();

        private int currentIndex = 0;
        private Coroutine cycleCoroutine;

        private void Reset()
        {
            childObjects.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                childObjects.Add(transform.GetChild(i).gameObject);
            }
        }

        private void Start()
        {
            if (startOnPlay)
                StartCycling();
        }

        public void StartCycling()
        {
            if (cycleCoroutine != null)
                StopCoroutine(cycleCoroutine);

            cycleCoroutine = StartCoroutine(CycleVisibilityRoutine());
        }

        public void StopCycling()
        {
            if (cycleCoroutine != null)
            {
                StopCoroutine(cycleCoroutine);
                cycleCoroutine = null;
            }
        }

        private IEnumerator CycleVisibilityRoutine()
        {
            while (true)
            {
                ShowOnlyCurrent();
                yield return new WaitForSeconds(showDuration);
                currentIndex = (currentIndex + 1) % childObjects.Count;
            }
        }

        private void ShowOnlyCurrent()
        {
            for (int i = 0; i < childObjects.Count; i++)
            {
                if (childObjects[i] != null)
                    childObjects[i].SetActive(i == currentIndex);
            }
        }
    }
}
