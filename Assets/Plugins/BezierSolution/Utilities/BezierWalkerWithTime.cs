using UnityEngine;
using UnityEngine.Events;

namespace BezierSolution
{
	public class BezierWalkerWithTime : MonoBehaviour, IBezierWalker
	{
		public enum TravelMode { Once, Loop, PingPong };

		private Transform cachedTransform;

		public BezierSpline spline;
		public TravelMode travelMode;

		public float travelTime = 5f;
		private float progress = 0f;

		public BezierSpline Spline { get { return spline; } }

		public float NormalizedT
		{
			get { return progress; }
			set { progress = value; }
		}

		public float movementLerpModifier = 10f;
		public float rotationLerpModifier = 10f;

		public bool lookForward = true;

		private bool isGoingForward = true;
		public bool MovingForward { get { return isGoingForward; } }

		public UnityEvent onPathCompleted = new UnityEvent();
		private bool onPathCompletedCalledAt1 = false;
		private bool onPathCompletedCalledAt0 = false;

		private void Awake()
		{
			cachedTransform = transform;
		}

		private void Update()
		{
			cachedTransform.position = Vector3.Lerp( cachedTransform.position, spline.GetPoint( progress ), movementLerpModifier * Time.deltaTime );

			if( lookForward )
			{
				Quaternion targetRotation;
				if( isGoingForward )
					targetRotation = Quaternion.LookRotation( spline.GetTangent( progress ) );
				else
					targetRotation = Quaternion.LookRotation( -spline.GetTangent( progress ) );

				cachedTransform.rotation = Quaternion.Lerp( cachedTransform.rotation, targetRotation, rotationLerpModifier * Time.deltaTime );
			}

			if( isGoingForward )
			{
				progress += Time.deltaTime / travelTime;

				if( progress > 1f )
				{
					if( !onPathCompletedCalledAt1 )
					{
						onPathCompleted.Invoke();
						onPathCompletedCalledAt1 = true;
					}

					if( travelMode == TravelMode.Once )
						progress = 1f;
					else if( travelMode == TravelMode.Loop )
						progress -= 1f;
					else
					{
						progress = 2f - progress;
						isGoingForward = false;
					}
				}
				else
				{
					onPathCompletedCalledAt1 = false;
				}
			}
			else
			{
				progress -= Time.deltaTime / travelTime;

				if( progress < 0f )
				{
					if( !onPathCompletedCalledAt0 )
					{
						onPathCompleted.Invoke();
						onPathCompletedCalledAt0 = true;
					}

					if( travelMode == TravelMode.Once )
						progress = 0f;
					else if( travelMode == TravelMode.Loop )
						progress += 1f;
					else
					{
						progress = -progress;
						isGoingForward = true;
					}
				}
				else
				{
					onPathCompletedCalledAt0 = false;
				}
			}
		}
	}
}