using UnityEngine;
using UnityEngine.Events;

namespace BezierSolution
{
	public enum TravelMode { Once = 0, Loop = 1, PingPong = 2 };
	public enum LookAtMode { None = 0, Forward = 1, SplineExtraData = 2 }

	public abstract class BezierWalker : MonoBehaviour
	{
		public abstract BezierSpline Spline { get; }
		public abstract bool MovingForward { get; set; }
		public abstract float NormalizedT { get; set; }

		public abstract void Execute( float deltaTime );

		public static readonly ExtraDataLerpFunction extraDataLerpAsQuaternionFunction = InterpolateExtraDataAsQuaternion;

		private static BezierPoint.ExtraData InterpolateExtraDataAsQuaternion( BezierPoint.ExtraData data1, BezierPoint.ExtraData data2, float normalizedT )
		{
			return Quaternion.LerpUnclamped( data1, data2, normalizedT );
		}

		protected void RotateTarget( Transform target, float normalizedT, LookAtMode lookAt, float lerpSpeed )
		{
			Quaternion targetRotation;
			switch( lookAt )
			{
				case LookAtMode.Forward:
				{
					BezierSpline.Segment segment = Spline.GetSegmentAt( normalizedT );
					targetRotation = Quaternion.LookRotation( MovingForward ? segment.GetTangent() : -segment.GetTangent(), segment.GetNormal() );
					break;
				}
				case LookAtMode.SplineExtraData: targetRotation = Spline.GetExtraData( normalizedT, extraDataLerpAsQuaternionFunction ); break;
				default: return;
			}

			target.rotation = Quaternion.Lerp( target.rotation, targetRotation, lerpSpeed );
		}

		protected void PostProcessMovement( TravelMode travelMode, ref bool onPathCompletedCalledAt0, ref bool onPathCompletedCalledAt1, UnityEvent onPathCompleted )
		{
			if( MovingForward )
			{
				if( NormalizedT >= 1f )
				{
					if( travelMode == TravelMode.Once )
						NormalizedT = 1f;
					else if( travelMode == TravelMode.Loop )
						NormalizedT -= 1f;
					else
					{
						NormalizedT = 2f - NormalizedT;
						MovingForward = !MovingForward;
					}

					if( !onPathCompletedCalledAt1 )
					{
						onPathCompletedCalledAt1 = true;
						onPathCompleted.Invoke();
					}
				}
				else
					onPathCompletedCalledAt1 = false;
			}
			else
			{
				if( NormalizedT <= 0f )
				{
					if( travelMode == TravelMode.Once )
						NormalizedT = 0f;
					else if( travelMode == TravelMode.Loop )
						NormalizedT += 1f;
					else
					{
						NormalizedT = -NormalizedT;
						MovingForward = !MovingForward;
					}

					if( !onPathCompletedCalledAt0 )
					{
						onPathCompletedCalledAt0 = true;
						onPathCompleted.Invoke();
					}
				}
				else
					onPathCompletedCalledAt0 = false;
			}
		}
	}
}