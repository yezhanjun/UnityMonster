using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT
{
    /// <summary>
    /// clock that shows by UI time to start/end round
    /// </summary>
    public class UITimer : MonoBehaviour
    {
        [SerializeField] Text _textTimer;
        public void UpdateTimer(int seconds)
        {
            //Mathf.FloorToInt((float))
            //Mathf.
            int minutes = 0;

            while (seconds >= 60)
            {
                seconds -= 60;
                minutes++;
            }
            _textTimer.text = minutes.ToString() + ":" + (seconds < 10 ? "0" : "") + seconds.ToString();
        }
    }
}