using UnityEngine;
using UnityEngine.Events;

namespace BezierSolution
{
	[AddComponentMenu( "Bezier Solution/Bezier Walker With Time" )]
	[HelpURL( "https://github.com/yasirkula/UnityBezierSolution" )]
	public class BezierWalkerWithTime : BezierWalker
	{
		public BezierSpline spline;
		public TravelMode travelMode;

		public float travelTime = 5f;
		[SerializeField]
		[Range( 0f, 1f )]
		private float m_normalizedT = 0f;

		public bool highQuality = false;

		public override BezierSpline Spline { get { return spline; } }

		public override float NormalizedT
		{
			get { return m_normalizedT; }
			set { m_normalizedT = value; }
		}

		public float movementLerpModifier = 10f;
		public float rotationLerpModifier = 10f;

		public LookAtMode lookAt = LookAtMode.ZForward;

		private bool isGoingForward = true;
		public override bool MovingForward
		{
			get { return isGoingForward; }
			set { isGoingForward = value; }
		}

		public UnityEvent onPathCompleted = new UnityEvent();
		private bool onPathCompletedCalledAt1 = false;
		private bool onPathCompletedCalledAt0 = false;

		private void Update()
		{
			Execute( Time.deltaTime );
		}

		public override void Execute( float deltaTime )
		{
			float _normalizedT = highQuality ? spline.evenlySpacedPoints.GetNormalizedTAtPercentage( m_normalizedT ) : m_normalizedT;
			transform.position = Vector3.Lerp( transform.position, spline.GetPoint( _normalizedT ), movementLerpModifier * deltaTime );
			RotateTarget( transform, _normalizedT, lookAt, rotationLerpModifier * deltaTime );

			m_normalizedT += ( isGoingForward ? deltaTime : -deltaTime ) / travelTime;
			PostProcessMovement( travelMode, ref onPathCompletedCalledAt0, ref onPathCompletedCalledAt1, onPathCompleted );
		}
	}
}