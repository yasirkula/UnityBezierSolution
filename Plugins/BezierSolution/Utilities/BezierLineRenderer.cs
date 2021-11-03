using UnityEngine;

namespace BezierSolution
{
	[AddComponentMenu( "Bezier Solution/Bezier Line Renderer" )]
	[HelpURL( "https://github.com/yasirkula/UnityBezierSolution" )]
	[RequireComponent( typeof( LineRenderer ) )]
	[ExecuteInEditMode]
	public class BezierLineRenderer : MonoBehaviour
	{
#pragma warning disable 0649
		[SerializeField]
		private BezierSpline m_spline;
		public BezierSpline spline
		{
			get { return m_spline; }
			set
			{
				if( m_spline != value )
				{
					if( m_spline )
						m_spline.onSplineChanged -= OnSplineChanged;

					m_spline = value;

					if( m_spline && isActiveAndEnabled )
					{
						m_spline.onSplineChanged -= OnSplineChanged;
						m_spline.onSplineChanged += OnSplineChanged;

						OnSplineChanged( m_spline, DirtyFlags.All );
					}
				}
			}
		}

		[SerializeField]
		[MinMaxRange( 0f, 1f )]
		private Vector2 m_splineSampleRange = new Vector2( 0f, 1f );
		public Vector2 SplineSampleRange
		{
			get { return m_splineSampleRange; }
			set
			{
				value.x = Mathf.Clamp01( value.x );
				value.y = Mathf.Clamp01( value.y );

				if( m_splineSampleRange != value )
				{
					m_splineSampleRange = value;

					if( isActiveAndEnabled )
						OnSplineChanged( m_spline, DirtyFlags.All );
				}
			}
		}

		[Header( "Line Options" )]
		[SerializeField]
		[Range( 0, 30 )]
		private int m_smoothness = 5;
		public int smoothness
		{
			get { return m_smoothness; }
			set
			{
				if( m_smoothness != value )
				{
					m_smoothness = value;

					if( isActiveAndEnabled )
						OnSplineChanged( m_spline, DirtyFlags.All );
				}
			}
		}

#if UNITY_EDITOR
		[Header( "Other Settings" )]
		[SerializeField]
		private bool executeInEditMode = false;

		[SerializeField, HideInInspector]
		private BezierSpline prevSpline;
#endif
#pragma warning restore 0649

		private LineRenderer lineRenderer;
		private Vector3[] lineRendererPoints;
#if UNITY_EDITOR
		private bool lineRendererUseWorldSpace = true;
#endif

		private void OnEnable()
		{
			if( m_spline )
			{
				m_spline.onSplineChanged -= OnSplineChanged;
				m_spline.onSplineChanged += OnSplineChanged;

				OnSplineChanged( m_spline, DirtyFlags.All );
			}
		}

		private void OnDisable()
		{
			if( m_spline )
				m_spline.onSplineChanged -= OnSplineChanged;
		}

#if UNITY_EDITOR
		private void Update()
		{
			if( lineRenderer && lineRenderer.useWorldSpace != lineRendererUseWorldSpace )
			{
				lineRendererUseWorldSpace = !lineRendererUseWorldSpace;

				if( isActiveAndEnabled )
					OnSplineChanged( m_spline, DirtyFlags.All );
			}
		}

		private void OnValidate()
		{
			BezierSpline _spline = m_spline;
			m_spline = prevSpline;
			spline = prevSpline = _spline;

			if( isActiveAndEnabled )
				OnSplineChanged( m_spline, DirtyFlags.All );
		}
#endif

		private void OnSplineChanged( BezierSpline spline, DirtyFlags dirtyFlags )
		{
#if UNITY_EDITOR
			if( !executeInEditMode && !UnityEditor.EditorApplication.isPlaying )
				return;
#endif

			if( ( dirtyFlags & DirtyFlags.SplineShapeChanged ) == DirtyFlags.SplineShapeChanged )
				Refresh( m_smoothness );
		}

		public void Refresh( int smoothness )
		{
			if( !m_spline || m_spline.Count < 2 )
				return;

			if( !lineRenderer )
				lineRenderer = GetComponent<LineRenderer>();

			smoothness = Mathf.Clamp( smoothness, 1, 30 );

			int numberOfPoints = ( m_spline.Count - 1 ) * smoothness;
			if( !m_spline.loop )
				numberOfPoints++; // spline.GetPoint( 1f )
			else
				numberOfPoints += smoothness; // Final point is connected to the first point via lineRenderer.loop, so no "numberOfPoints++" here

			if( lineRendererPoints == null || lineRendererPoints.Length != numberOfPoints )
				lineRendererPoints = new Vector3[numberOfPoints];

			if( m_splineSampleRange.x <= 0f && m_splineSampleRange.y >= 1f )
			{
				int pointIndex = 0;
				float smoothnessStep = 1f / smoothness;
				for( int i = 0; i < m_spline.Count - 1; i++ )
				{
					BezierSpline.Segment segment = new BezierSpline.Segment( m_spline[i], m_spline[i + 1], 0f );
					for( int j = 0; j < smoothness; j++, pointIndex++ )
						lineRendererPoints[pointIndex] = segment.GetPoint( j * smoothnessStep );
				}

				if( !m_spline.loop )
					lineRendererPoints[numberOfPoints - 1] = m_spline.GetPoint( 1f );
				else
				{
					BezierSpline.Segment segment = new BezierSpline.Segment( m_spline[m_spline.Count - 1], m_spline[0], 0f );
					for( int j = 0; j < smoothness; j++, pointIndex++ )
						lineRendererPoints[pointIndex] = segment.GetPoint( j * smoothnessStep );
				}
			}
			else
			{
				float smoothnessStep = ( m_splineSampleRange.y - m_splineSampleRange.x ) / ( numberOfPoints - 1 );
				for( int i = 0; i < numberOfPoints; i++ )
					lineRendererPoints[i] = spline.GetPoint( m_splineSampleRange.x + i * smoothnessStep );
			}

#if UNITY_EDITOR
			lineRendererUseWorldSpace = lineRenderer.useWorldSpace;
#endif
			if( !lineRenderer.useWorldSpace )
			{
				Vector3 initialPoint = m_spline.GetPoint( 0f );
				for( int i = 0; i < numberOfPoints; i++ )
					lineRendererPoints[i] -= initialPoint;
			}

			lineRenderer.positionCount = lineRendererPoints.Length;
			lineRenderer.SetPositions( lineRendererPoints );
			lineRenderer.loop = m_spline.loop && m_splineSampleRange.x <= 0f && m_splineSampleRange.y >= 1f;

		}
	}
}