using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MTPSKIT
{
    /// <summary>
    /// class with useful static methods used across MTPS KIT
    /// </summary>

    public static class GameTools
    {
        /// <summary>
        /// This method lets create hitstan than will omit given object, useful for shooting
        /// </summary>
        /// <param name="barrel"></param>
        /// <param name="parent"> Object that have to be excluded from raycast reach </param>
        /// <param name="layer"></param>
        /// <param name="range"></param>
        /// <returns></returns>
        public static RaycastHit[] HitScan(Vector3 pos, Vector3 dir, Transform parent, int layer, float range = 250f) 
        {
            Ray rayFire = new Ray(pos, dir);
            RaycastHit[] hittedObjects = Physics.RaycastAll(rayFire, range, layer).OrderBy(e => e.distance).ToArray();
            List<RaycastHit> approvedObjects = hittedObjects.ToList();
            if (hittedObjects != null)
            {
                foreach (RaycastHit _hit in hittedObjects)
                {
                    if (_hit.transform.root == parent)
                        approvedObjects.Remove(_hit);
                }
            }
            return approvedObjects.ToArray();
        }
        public static RaycastHit[] HitScan(Transform barrel, Transform parent, int layer, float range = 250f) =>
            HitScan(barrel.position, barrel.forward, parent, layer, range);


        public static Transform GetChildByName(GameObject _parent, string _childName)
        {
            foreach (Transform trans in _parent.GetComponentsInChildren<Transform>(true))
            {
                if (trans.name == _childName)
                {
                    return trans.transform;
                }
            }
            return null;
        }
        /// <summary>
        /// set layer for object and all of its children
        /// </summary>
        /// <param name="go"></param>
        /// <param name="layerNumber"></param>
        public static void SetLayerRecursively(GameObject go, int layerNumber)
        {
            foreach (Transform trans in go.GetComponentsInChildren<Transform>(true))
            {
                trans.gameObject.layer = layerNumber;
            }
        }

        #region chat formatters
        /// <summary>
        /// cut message to 190 characters
        /// </summary>
        public static string CheckMessageLength(string _message)
        {
            int charCount = 0;
            string newMassage = "";
            foreach (char c in _message)
            {
                newMassage += c;
                charCount++;
                if (charCount >= 190) return newMassage;
            }
            return _message;
        }
        public static string DeleteLines(string s, int linesToRemove)
        {
            return s.Split(Environment.NewLine.ToCharArray(),
                           linesToRemove + 1
                ).Skip(linesToRemove)
                .FirstOrDefault();
        }
        public static int GetLineCount(string input)
        {
            int lineCount = 0;

            for (int i = 0; i < input.Length; i++)
            {
                switch (input[i])
                {
                    case '\r':
                        {
                            if (i + 1 < input.Length)
                            {
                                i++;
                                if (input[i] == '\r')
                                {
                                    lineCount += 2;
                                }
                                else
                                {
                                    lineCount++;
                                }
                            }
                            else
                            {

                                lineCount++;
                            }
                        }
                        break;
                    case '\n':
                        lineCount++;
                        break;
                    default:
                        break;
                }
            }
            return lineCount;
        }
        #endregion
    }
}