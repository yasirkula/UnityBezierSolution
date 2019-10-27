using UnityEngine;

namespace BezierSolution
{
	public enum TravelMode { Once = 0, Loop = 1, PingPong = 2 };
	public enum LookAtMode { None = 0, Forward = 1, SplineExtraData = 2 }

	public abstract class BezierWalker : MonoBehaviour
	{
		public abstract BezierSpline Spline { get; }
		public abstract bool MovingForward { get; }
		public abstract float NormalizedT { get; set; }

		public abstract void Execute( float deltaTime );
	}
}