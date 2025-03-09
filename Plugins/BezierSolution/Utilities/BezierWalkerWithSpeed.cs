using UnityEngine;
using UnityEngine.Events;

namespace BezierSolution
{
	[AddComponentMenu( "Bezier Solution/Bezier Walker With Speed" )]
	[HelpURL( "https://github.com/yasirkula/UnityBezierSolution" )]
	public class BezierWalkerWithSpeed : BezierWalker
	{
		public BezierSpline spline;
		public TravelMode travelMode;

		public float speed = 5f;
		[SerializeField]
		[Range( 0f, 1f )]
		private float m_normalizedT = 0f;

		public override BezierSpline Spline { get { return spline; } }

		public override float NormalizedT
		{
			get { return m_normalizedT; }
			set { m_normalizedT = value; }
		}

		//public float movementLerpModifier = 10f;
		public float rotationLerpModifier = 10f;

		public LookAtMode lookAt = LookAtMode.ZForward;

		private bool isGoingForward = true;
		public override bool MovingForward
		{
			get { return ( speed >= 0f ) == isGoingForward; }
			set { isGoingForward = ( speed >= 0f ) == value; }
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
			transform.position = spline.MoveAlongSpline( ref m_normalizedT, ( isGoingForward ? speed : -speed ) * deltaTime );
			RotateTarget( transform, m_normalizedT, lookAt, rotationLerpModifier * deltaTime );
			PostProcessMovement( travelMode, ref onPathCompletedCalledAt0, ref onPathCompletedCalledAt1, onPathCompleted );
		}
	}
}