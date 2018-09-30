using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BezierSolution
{
    public class BezierPercentageInterpolation : MonoBehaviour
    {
        public BezierSpline spline = null;
        [Range(20, 200), SerializeField]
        private float cacheQuality = 100;
        [Range(0, 1)]
        public float progress = 0f;
        [Range(0,20)]
        public int smoothness = 0;

        private bool cacheCalculated = true;
        private float[] cache;

        //============//
        // BEHAVIOURS //
        //============//
        private void Start()
        {
            PrepareCache();
        }

        private void Update()
        {
            InterpolatePositionAndRotation(progress);

            if (cacheCalculated == false)
                PrepareCache();

            if (progress > 1)
                progress = 0;
        }

        //========//
        // PUBLIC //
        //========//
        public void SetCacheQuality(float quality)
        {
            if (Mathf.Approximately(quality,cacheQuality))
                return;

            cacheCalculated = false;
            cacheQuality = quality;
        }

        //=========//
        // HELPERS //
        //=========//
        private void InterpolatePositionAndRotation(float percent)
        {
            float normalizedT = MapPercentToBeziereRatio(progress);

            if (smoothness == 0)
            {
                transform.position = spline.GetPoint(normalizedT);
                transform.rotation = Quaternion.LookRotation(spline.GetTangent(normalizedT));
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, spline.GetPoint(normalizedT), Time.deltaTime * 100 / smoothness);
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(spline.GetTangent(normalizedT)), Time.deltaTime * 100 / smoothness);
            }
        }

        private float MapPercentToBeziereRatio(float percent)
        {
            if (percent >= 1)
                return 1f;
            
            PrepareCache();
            percent = Mathf.Clamp(percent, 0, 1);

            int cacheNum = cache.Length;
            int leftIndex = (int)(progress * cacheNum);
            int rightIndex = (leftIndex == cacheNum - 1) ? leftIndex : leftIndex + 1;
            float remainingIndex = progress * cacheNum - leftIndex;

            return Mathf.Lerp(cache[leftIndex], cache[rightIndex], remainingIndex);
        }

        private void PrepareCache()
        {
            if (!cacheCalculated)
                return;

            cacheCalculated = false;

            List<float> list = new List<float>();
            float percent = 0;
            float detail = 1 / cacheQuality;

            do
            {
                list.Add(percent);
                spline.MoveAlongSpline(ref percent, detail);
            }
            while (percent < 1);

            cache = list.ToArray();
            Debug.Log("completed caching with size of " + cache.Length);
        }

    }
}
