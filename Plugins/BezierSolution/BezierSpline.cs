using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_2017_3_OR_NEWER
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo( "BezierSolution.Editor" )]
#else
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo( "Assembly-CSharp-Editor" )]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo( "Assembly-CSharp-Editor-firstpass" )]
#endif
namespace BezierSolution
{
	[AddComponentMenu( "Bezier Solution/Bezier Spline" )]
	[HelpURL( "https://github.com/yasirkula/UnityBezierSolution" )]
	[ExecuteInEditMode]
	public partial class BezierSpline : MonoBehaviour, IEnumerable<BezierPoint>
	{
		internal List<BezierPoint> endPoints = new List<BezierPoint>(); // This is not readonly because otherwise BezierWalkers' "Simulate In Editor" functionality may break after recompilation

		public int Count { get { return endPoints.Count; } }
		public BezierPoint this[int index] { get { return endPoints[index]; } }

		private float? m_length = null;
		public float length
		{
			get
			{
				if( m_length == null )
					m_length = GetLengthApproximately( 0f, 1f );

				return m_length.Value;
			}
		}

		[System.Obsolete( "Length is renamed to length" )]
		public float Length { get { return length; } }

		[SerializeField, HideInInspector]
		[UnityEngine.Serialization.FormerlySerializedAs( "loop" )]
		private bool m_loop = false;
		public bool loop
		{
			get { return m_loop; }
			set
			{
				if( m_loop != value )
				{
					m_loop = value;
					dirtyFlags |= InternalDirtyFlags.All;
				}
			}
		}

		public bool drawGizmos = false;
		public Color gizmoColor = Color.white;
		[UnityEngine.Serialization.FormerlySerializedAs( "m_gizmoSmoothness" )]
		public int gizmoSmoothness = 4;

		private static Material gizmoMaterial;

		[SerializeField, HideInInspector]
		[UnityEngine.Serialization.FormerlySerializedAs( "Internal_AutoConstructMode" )]
		private SplineAutoConstructMode m_autoConstructMode = SplineAutoConstructMode.None;
		public SplineAutoConstructMode autoConstructMode
		{
			get { return m_autoConstructMode; }
			set
			{
				if( m_autoConstructMode != value )
				{
					m_autoConstructMode = value;

					if( value != SplineAutoConstructMode.None )
						dirtyFlags |= InternalDirtyFlags.EndPointTransformChange | InternalDirtyFlags.ControlPointPositionChange;
				}
			}
		}

		private Vector3[] autoConstructedSplineRhs;
		private Vector3[] autoConstructedSplineControlPoints;
		private float[] autoConstructedSplineTmp;

		[SerializeField, HideInInspector]
		[UnityEngine.Serialization.FormerlySerializedAs( "Internal_AutoCalculateNormals" )]
		private bool m_autoCalculateNormals = false;
		public bool autoCalculateNormals
		{
			get { return m_autoCalculateNormals; }
			set
			{
				if( m_autoCalculateNormals != value )
				{
					m_autoCalculateNormals = value;
					dirtyFlags |= InternalDirtyFlags.NormalOffsetChange;
				}
			}
		}

		[SerializeField, HideInInspector]
		[UnityEngine.Serialization.FormerlySerializedAs( "Internal_AutoCalculatedNormalsAngle" )]
		private float m_autoCalculatedNormalsAngle = 0f;
		public float autoCalculatedNormalsAngle
		{
			get { return m_autoCalculatedNormalsAngle; }
			set
			{
				if( m_autoCalculatedNormalsAngle != value )
				{
					m_autoCalculatedNormalsAngle = value;
					dirtyFlags |= InternalDirtyFlags.NormalOffsetChange;
				}
			}
		}

		[SerializeField, HideInInspector]
		private int m_autoCalculatedIntermediateNormalsCount = 10;
		public int autoCalculatedIntermediateNormalsCount
		{
			get { return m_autoCalculatedIntermediateNormalsCount; }
			set
			{
				value = Mathf.Clamp( value, 0, 999 );

				if( m_autoCalculatedIntermediateNormalsCount != value )
				{
					m_autoCalculatedIntermediateNormalsCount = value;
					dirtyFlags |= InternalDirtyFlags.NormalOffsetChange;
				}
			}
		}

		private EvenlySpacedPointsHolder m_evenlySpacedPoints = null;
		public EvenlySpacedPointsHolder evenlySpacedPoints
		{
			get
			{
				if( m_evenlySpacedPoints == null )
					m_evenlySpacedPoints = CalculateEvenlySpacedPoints( m_evenlySpacedPointsResolution, m_evenlySpacedPointsAccuracy );

				return m_evenlySpacedPoints;
			}
		}

		private PointCache m_pointCache = null;
		public PointCache pointCache
		{
			get
			{
				if( m_pointCache == null )
					m_pointCache = GeneratePointCache( resolution: m_pointCacheResolution );

				return m_pointCache;
			}
		}

		[SerializeField, HideInInspector]
		private float m_evenlySpacedPointsResolution = 10f;
		public float evenlySpacedPointsResolution
		{
			get { return m_evenlySpacedPointsResolution; }
			set
			{
				value = Mathf.Clamp( value, 1f, 999f );

				if( m_evenlySpacedPointsResolution != value )
				{
					m_evenlySpacedPointsResolution = value;
					m_evenlySpacedPoints = null;

					dirtyFlags = InternalDirtyFlags.All;
				}
			}
		}

		[SerializeField, HideInInspector]
		private float m_evenlySpacedPointsAccuracy = 3f;
		public float evenlySpacedPointsAccuracy
		{
			get { return m_evenlySpacedPointsAccuracy; }
			set
			{
				value = Mathf.Clamp( value, 1f, 999f );

				if( m_evenlySpacedPointsAccuracy != value )
				{
					m_evenlySpacedPointsAccuracy = value;
					m_evenlySpacedPoints = null;

					dirtyFlags = InternalDirtyFlags.All;
				}
			}
		}

		[SerializeField, HideInInspector]
		private int m_pointCacheResolution = 100;
		public int pointCacheResolution
		{
			get { return m_pointCacheResolution; }
			set
			{
				value = Mathf.Clamp( value, 10, 10000 );

				if( m_pointCacheResolution != value )
				{
					m_pointCacheResolution = value;
					m_pointCache = null;

					dirtyFlags = InternalDirtyFlags.All;
				}
			}
		}

		public event SplineChangeDelegate onSplineChanged;

		public int version { get; private set; }

		internal InternalDirtyFlags dirtyFlags;

		private void Awake()
		{
			Refresh();
		}

#if UNITY_EDITOR
		private void OnTransformChildrenChanged()
		{
			dirtyFlags |= InternalDirtyFlags.All;
			Refresh();
		}
#endif

		private void LateUpdate()
		{
			CheckDirty();
		}

		internal void CheckDirty()
		{
			for( int i = 0; i < endPoints.Count; i++ )
				endPoints[i].RefreshIfChanged();

			if( dirtyFlags != InternalDirtyFlags.None && endPoints.Count >= 2 )
			{
				DirtyFlags publishedDirtyFlags = DirtyFlags.None;

				if( ( dirtyFlags & InternalDirtyFlags.ExtraDataChange ) == InternalDirtyFlags.ExtraDataChange )
					publishedDirtyFlags |= DirtyFlags.ExtraDataChanged;

				if( ( dirtyFlags & ( InternalDirtyFlags.EndPointTransformChange | InternalDirtyFlags.ControlPointPositionChange ) ) != InternalDirtyFlags.None )
				{
					if( m_autoConstructMode == SplineAutoConstructMode.None )
						publishedDirtyFlags |= DirtyFlags.SplineShapeChanged;
					else
					{
						switch( m_autoConstructMode )
						{
							case SplineAutoConstructMode.Linear: ConstructLinearPath(); break;
							case SplineAutoConstructMode.Smooth1: AutoConstructSpline(); break;
							case SplineAutoConstructMode.Smooth2: AutoConstructSpline2(); break;
						}

						// If a control point position was changed only, we've reverted that change by auto constructing the spline again
						dirtyFlags &= ~InternalDirtyFlags.ControlPointPositionChange;

						// If an end point's position was changed, then the spline's shape has indeed changed
						if( ( dirtyFlags & InternalDirtyFlags.EndPointTransformChange ) == InternalDirtyFlags.EndPointTransformChange )
							publishedDirtyFlags |= DirtyFlags.SplineShapeChanged;
					}
				}

				if( ( dirtyFlags & ( InternalDirtyFlags.NormalChange | InternalDirtyFlags.NormalOffsetChange | InternalDirtyFlags.EndPointTransformChange | InternalDirtyFlags.ControlPointPositionChange ) ) != InternalDirtyFlags.None )
				{
					if( !m_autoCalculateNormals )
					{
						// Normals are actually changed only when NormalChange flag is on
						if( ( dirtyFlags & InternalDirtyFlags.NormalChange ) == InternalDirtyFlags.NormalChange )
							publishedDirtyFlags |= DirtyFlags.NormalsChanged;
					}
					else
					{
						if( m_autoCalculatedIntermediateNormalsCount <= 0 )
							AutoCalculateNormals( m_autoCalculatedNormalsAngle, calculateIntermediateNormals: false );
						else
							AutoCalculateNormals( m_autoCalculatedNormalsAngle, m_autoCalculatedIntermediateNormalsCount + 1, true );

						// If an end point's only normal vector was changed, we've reverted that change by auto calculating the normals again
						dirtyFlags &= ~InternalDirtyFlags.NormalChange;

						// If an end point's position or normal calculation offset was changed, then the spline's normals have indeed changed
						if( ( dirtyFlags & ( InternalDirtyFlags.NormalOffsetChange | InternalDirtyFlags.EndPointTransformChange | InternalDirtyFlags.ControlPointPositionChange ) ) != InternalDirtyFlags.None )
							publishedDirtyFlags |= DirtyFlags.NormalsChanged;
					}
				}

				if( ( publishedDirtyFlags & DirtyFlags.SplineShapeChanged ) == DirtyFlags.SplineShapeChanged )
				{
					m_length = null;
					m_evenlySpacedPoints = null;
				}

				m_pointCache = null;
				version++;

				if( onSplineChanged != null )
				{
					try
					{
						onSplineChanged( this, publishedDirtyFlags );
					}
					catch( System.Exception e )
					{
						Debug.LogException( e );
					}
				}
			}

			dirtyFlags = InternalDirtyFlags.None;
		}

		public void Initialize( int endPointsCount )
		{
			if( endPointsCount < 2 )
			{
				Debug.LogError( "Can't initialize spline with " + endPointsCount + " point(s). At least 2 points are needed" );
				return;
			}

			// Destroy current end points
			endPoints.Clear();
			GetComponentsInChildren( endPoints );

			for( int i = endPoints.Count - 1; i >= 0; i-- )
				DestroyImmediate( endPoints[i].gameObject );

			// Create new end points
			endPoints.Clear();

			for( int i = 0; i < endPointsCount; i++ )
				InsertNewPointAt( i );

			Refresh();
		}

		public void Refresh()
		{
			endPoints.Clear();
			GetComponentsInChildren( endPoints );

			for( int i = 0; i < endPoints.Count; i++ )
			{
				endPoints[i].spline = this;
				endPoints[i].index = i;
			}

			CheckDirty();
		}

		public BezierPoint InsertNewPointAt( int index )
		{
			if( index < 0 || index > endPoints.Count )
			{
				Debug.LogError( "Index " + index + " is out of range: [0," + endPoints.Count + "]" );
				return null;
			}

			int prevCount = endPoints.Count;
			BezierPoint point = new GameObject( "Point" ).AddComponent<BezierPoint>();
			point.spline = this;

			Transform parent = endPoints.Count == 0 ? transform : ( index == 0 ? endPoints[0].transform.parent : endPoints[index - 1].transform.parent );
			int siblingIndex = index == 0 ? 0 : endPoints[index - 1].transform.GetSiblingIndex() + 1;
			point.transform.SetParent( parent, false );
			point.transform.SetSiblingIndex( siblingIndex );

			if( endPoints.Count == prevCount ) // If spline isn't automatically Refresh()'ed
				endPoints.Insert( index, point );

			for( int i = index; i < endPoints.Count; i++ )
				endPoints[i].index = i;

			dirtyFlags |= InternalDirtyFlags.All;

			return point;
		}

		public BezierPoint DuplicatePointAt( int index )
		{
			if( index < 0 || index >= endPoints.Count )
			{
				Debug.LogError( "Index " + index + " is out of range: [0," + ( endPoints.Count - 1 ) + "]" );
				return null;
			}

			BezierPoint newPoint = InsertNewPointAt( index + 1 );
			endPoints[index].CopyTo( newPoint );

			return newPoint;
		}

		public void RemovePointAt( int index )
		{
			if( endPoints.Count <= 2 )
			{
				Debug.LogError( "Can't remove point: spline must consist of at least two points!" );
				return;
			}

			if( index < 0 || index >= endPoints.Count )
			{
				Debug.LogError( "Index " + index + " is out of range: [0," + endPoints.Count + ")" );
				return;
			}

			BezierPoint point = endPoints[index];
			endPoints.RemoveAt( index );

			for( int i = index; i < endPoints.Count; i++ )
				endPoints[i].index = i;

			DestroyImmediate( point.gameObject );

			dirtyFlags |= InternalDirtyFlags.All;
		}

		public void SwapPointsAt( int index1, int index2 )
		{
			if( index1 == index2 )
				return;

			if( index1 < 0 || index1 >= endPoints.Count || index2 < 0 || index2 >= endPoints.Count )
			{
				Debug.LogError( "Indices must be in range [0," + ( endPoints.Count - 1 ) + "]" );
				return;
			}

			BezierPoint point1 = endPoints[index1];
			BezierPoint point2 = endPoints[index2];

			int point1SiblingIndex = point1.transform.GetSiblingIndex();
			int point2SiblingIndex = point2.transform.GetSiblingIndex();

			Transform point1Parent = point1.transform.parent;
			Transform point2Parent = point2.transform.parent;

			endPoints[index1] = point2;
			endPoints[index2] = point1;

			point1.index = index2;
			point2.index = index1;

			if( point1Parent != point2Parent )
			{
				point1.transform.SetParent( point2Parent, true );
				point2.transform.SetParent( point1Parent, true );
			}

			point1.transform.SetSiblingIndex( point2SiblingIndex );
			point2.transform.SetSiblingIndex( point1SiblingIndex );

			dirtyFlags |= InternalDirtyFlags.All;
		}

		public void ChangePointIndex( int previousIndex, int newIndex )
		{
			ChangePointIndex( previousIndex, newIndex, null );
		}

		internal void ChangePointIndex( int previousIndex, int newIndex, string undo )
		{
			if( previousIndex == newIndex )
				return;

			if( previousIndex < 0 || previousIndex >= endPoints.Count || newIndex < 0 || newIndex >= endPoints.Count )
			{
				Debug.LogError( "Indices must be in range [0," + ( endPoints.Count - 1 ) + "]" );
				return;
			}

			BezierPoint point1 = endPoints[previousIndex];
			BezierPoint point2 = endPoints[newIndex];

#if UNITY_EDITOR
			if( undo != null )
				UnityEditor.Undo.RegisterCompleteObjectUndo( point1.transform.parent, undo );
#endif

			if( previousIndex < newIndex )
			{
				for( int i = previousIndex; i < newIndex; i++ )
					endPoints[i] = endPoints[i + 1];
			}
			else
			{
				for( int i = previousIndex; i > newIndex; i-- )
					endPoints[i] = endPoints[i - 1];
			}

			endPoints[newIndex] = point1;

			Transform point2Parent = point2.transform.parent;
			if( point1.transform.parent != point2Parent )
			{
#if UNITY_EDITOR
				if( undo != null )
				{
					UnityEditor.Undo.SetTransformParent( point1.transform, point2Parent, undo );
					UnityEditor.Undo.RegisterCompleteObjectUndo( point2Parent, undo );
				}
				else
#endif
					point1.transform.SetParent( point2Parent, true );

				int point2SiblingIndex = point2.transform.GetSiblingIndex();
				if( previousIndex < newIndex )
				{
					if( point1.transform.GetSiblingIndex() < point2SiblingIndex )
						point1.transform.SetSiblingIndex( point2SiblingIndex );
					else
						point1.transform.SetSiblingIndex( point2SiblingIndex + 1 );
				}
				else
				{
					if( point1.transform.GetSiblingIndex() < point2SiblingIndex )
						point1.transform.SetSiblingIndex( point2SiblingIndex - 1 );
					else
						point1.transform.SetSiblingIndex( point2SiblingIndex );
				}
			}
			else
				point1.transform.SetSiblingIndex( point2.transform.GetSiblingIndex() );

			for( int i = 0; i < endPoints.Count; i++ )
				endPoints[i].index = i;

			dirtyFlags |= InternalDirtyFlags.All;
		}

		public void InvertSpline()
		{
			InvertSpline( null );
		}

		internal void InvertSpline( string undo )
		{
#if UNITY_EDITOR
			// In Editor, this.endPoints will change at each for-iteration due to OnTransformChildrenChanged
			// but we need the list to be immutable while this function is being executed
			List<BezierPoint> endPoints = new List<BezierPoint>( this.endPoints );
#endif

			endPoints.Reverse();

			for( int i = endPoints.Count / 2 - 1; i >= 0; i-- )
			{
				BezierPoint point1 = endPoints[i];
				BezierPoint point2 = endPoints[endPoints.Count - 1 - i];

				int point1SiblingIndex = point1.transform.GetSiblingIndex();
				int point2SiblingIndex = point2.transform.GetSiblingIndex();

				Transform point1Parent = point1.transform.parent;
				Transform point2Parent = point2.transform.parent;

#if UNITY_EDITOR
				if( undo != null )
				{
					UnityEditor.Undo.RegisterCompleteObjectUndo( point1Parent, undo );
					UnityEditor.Undo.RegisterCompleteObjectUndo( point2Parent, undo );
				}
#endif

				if( point1Parent != point2Parent )
				{
#if UNITY_EDITOR
					if( undo != null )
					{
						UnityEditor.Undo.SetTransformParent( point1.transform, point2Parent, undo );
						UnityEditor.Undo.SetTransformParent( point2.transform, point1Parent, undo );
					}
					else
#endif
					{
						point1.transform.SetParent( point2Parent, true );
						point2.transform.SetParent( point1Parent, true );
					}
				}

				point1.transform.SetSiblingIndex( point2SiblingIndex );
				point2.transform.SetSiblingIndex( point1SiblingIndex );
			}

			for( int i = 0; i < endPoints.Count; i++ )
			{
#if UNITY_EDITOR
				if( undo != null )
					UnityEditor.Undo.RecordObject( endPoints[i], undo );
#endif

				// Swap control points
				Vector3 precedingControlPointLocalPosition = endPoints[i].precedingControlPointLocalPosition;
				endPoints[i].precedingControlPointLocalPosition = endPoints[i].followingControlPointLocalPosition;
				endPoints[i].followingControlPointLocalPosition = precedingControlPointLocalPosition;

				endPoints[i].index = i;
			}

#if UNITY_EDITOR
			this.endPoints = endPoints;
#endif

			dirtyFlags |= InternalDirtyFlags.All;
		}

		public Vector3 GetPoint( float normalizedT )
		{
			if( !m_loop )
			{
				if( normalizedT <= 0f )
					return endPoints[0].position;
				else if( normalizedT >= 1f )
					return endPoints[endPoints.Count - 1].position;
			}
			else
			{
				while( normalizedT < 0f )
					normalizedT += 1f;
				while( normalizedT >= 1f )
					normalizedT -= 1f;
			}

			float t = normalizedT * ( m_loop ? endPoints.Count : ( endPoints.Count - 1 ) );

			BezierPoint startPoint, endPoint;

			int startIndex = (int) t;
			int endIndex = startIndex + 1;

			if( endIndex == endPoints.Count )
				endIndex = 0;

			startPoint = endPoints[startIndex];
			endPoint = endPoints[endIndex];

			float localT = t - startIndex;
			float oneMinusLocalT = 1f - localT;

			return oneMinusLocalT * oneMinusLocalT * oneMinusLocalT * startPoint.position +
				   3f * oneMinusLocalT * oneMinusLocalT * localT * startPoint.followingControlPointPosition +
				   3f * oneMinusLocalT * localT * localT * endPoint.precedingControlPointPosition +
				   localT * localT * localT * endPoint.position;
		}

		public Vector3 GetTangent( float normalizedT )
		{
			if( !m_loop )
			{
				if( normalizedT <= 0f )
					return 3f * ( endPoints[0].followingControlPointPosition - endPoints[0].position );
				else if( normalizedT >= 1f )
				{
					int index = endPoints.Count - 1;
					return 3f * ( endPoints[index].position - endPoints[index].precedingControlPointPosition );
				}
			}
			else
			{
				while( normalizedT < 0f )
					normalizedT += 1f;
				while( normalizedT >= 1f )
					normalizedT -= 1f;
			}

			float t = normalizedT * ( m_loop ? endPoints.Count : ( endPoints.Count - 1 ) );

			BezierPoint startPoint, endPoint;

			int startIndex = (int) t;
			int endIndex = startIndex + 1;

			if( endIndex == endPoints.Count )
				endIndex = 0;

			startPoint = endPoints[startIndex];
			endPoint = endPoints[endIndex];

			float localT = t - startIndex;
			float oneMinusLocalT = 1f - localT;

			return 3f * oneMinusLocalT * oneMinusLocalT * ( startPoint.followingControlPointPosition - startPoint.position ) +
				   6f * oneMinusLocalT * localT * ( endPoint.precedingControlPointPosition - startPoint.followingControlPointPosition ) +
				   3f * localT * localT * ( endPoint.position - endPoint.precedingControlPointPosition );
		}

		public Vector3 GetNormal( float normalizedT )
		{
			if( !m_loop )
			{
				if( normalizedT <= 0f )
					return endPoints[0].normal;
				else if( normalizedT >= 1f )
					return endPoints[endPoints.Count - 1].normal;
			}
			else
			{
				while( normalizedT < 0f )
					normalizedT += 1f;
				while( normalizedT >= 1f )
					normalizedT -= 1f;
			}

			float t = normalizedT * ( m_loop ? endPoints.Count : ( endPoints.Count - 1 ) );

			int startIndex = (int) t;
			int endIndex = startIndex + 1;

			if( endIndex == endPoints.Count )
				endIndex = 0;

			float localT = t - startIndex;

			Vector3[] intermediateNormals = endPoints[startIndex].intermediateNormals;
			if( intermediateNormals != null && intermediateNormals.Length > 0 )
			{
				localT *= intermediateNormals.Length - 1;
				int localStartIndex = (int) localT;

				return ( localStartIndex < intermediateNormals.Length - 1 ) ? Vector3.LerpUnclamped( intermediateNormals[localStartIndex], intermediateNormals[localStartIndex + 1], localT - localStartIndex ) : intermediateNormals[localStartIndex];
			}

			Vector3 startNormal = endPoints[startIndex].normal;
			Vector3 endNormal = endPoints[endIndex].normal;

			Vector3 normal = Vector3.LerpUnclamped( startNormal, endNormal, localT );
			if( normal.y == 0f && normal.x == 0f && normal.z == 0f )
			{
				// Don't return Vector3.zero as normal
				normal = Vector3.LerpUnclamped( startNormal, endNormal, localT > 0.01f ? ( localT - 0.01f ) : ( localT + 0.01f ) );
				if( normal.y == 0f && normal.x == 0f && normal.z == 0f )
					normal = Vector3.up;
			}

			return normal;
		}

		public BezierPoint.ExtraData GetExtraData( float normalizedT )
		{
			return GetExtraData( normalizedT, defaultExtraDataLerpFunction );
		}

		public BezierPoint.ExtraData GetExtraData( float normalizedT, ExtraDataLerpFunction lerpFunction )
		{
			if( !m_loop )
			{
				if( normalizedT <= 0f )
					return endPoints[0].extraData;
				else if( normalizedT >= 1f )
					return endPoints[endPoints.Count - 1].extraData;
			}
			else
			{
				while( normalizedT < 0f )
					normalizedT += 1f;
				while( normalizedT >= 1f )
					normalizedT -= 1f;
			}

			float t = normalizedT * ( m_loop ? endPoints.Count : ( endPoints.Count - 1 ) );

			int startIndex = (int) t;
			int endIndex = startIndex + 1;

			if( endIndex == endPoints.Count )
				endIndex = 0;

			return lerpFunction( endPoints[startIndex].extraData, endPoints[endIndex].extraData, t - startIndex );
		}

		public float GetLengthApproximately( float startNormalizedT, float endNormalizedT, float accuracy = 50f )
		{
			if( endNormalizedT < startNormalizedT )
			{
				float temp = startNormalizedT;
				startNormalizedT = endNormalizedT;
				endNormalizedT = temp;
			}

			if( startNormalizedT < 0f )
				startNormalizedT = 0f;

			if( endNormalizedT > 1f )
				endNormalizedT = 1f;

			float step = AccuracyToStepSize( accuracy ) * ( endNormalizedT - startNormalizedT );

			float length = 0f;
			Vector3 prevPoint = GetPoint( startNormalizedT );
			for( float i = startNormalizedT + step; i < endNormalizedT; i += step )
			{
				Vector3 thisPoint = GetPoint( i );
				length += Vector3.Distance( thisPoint, prevPoint );
				prevPoint = thisPoint;
			}

			length += Vector3.Distance( prevPoint, GetPoint( endNormalizedT ) );

			return length;
		}

		public Segment GetSegmentAt( float normalizedT )
		{
			if( !m_loop )
			{
				if( normalizedT <= 0f )
					return new Segment( endPoints[0], endPoints[1], 0f );
				else if( normalizedT >= 1f )
					return new Segment( endPoints[endPoints.Count - 2], endPoints[endPoints.Count - 1], 1f );
			}
			else
			{
				while( normalizedT < 0f )
					normalizedT += 1f;
				while( normalizedT >= 1f )
					normalizedT -= 1f;
			}

			float t = normalizedT * ( m_loop ? endPoints.Count : ( endPoints.Count - 1 ) );

			int startIndex = (int) t;
			int endIndex = startIndex + 1;

			if( endIndex == endPoints.Count )
				endIndex = 0;

			return new Segment( endPoints[startIndex], endPoints[endIndex], t - startIndex );
		}

		[System.Obsolete( "GetNearestPointIndicesTo is renamed to GetSegmentAt" )]
		public Segment GetNearestPointIndicesTo( float normalizedT )
		{
			return GetSegmentAt( normalizedT );
		}

		public Vector3 FindNearestPointTo( Vector3 worldPos, float accuracy = 100f, int secondPassIterations = 7, float secondPassExtents = 0.025f )
		{
			float normalizedT;
			return FindNearestPointTo( worldPos, out normalizedT, accuracy, secondPassIterations, secondPassExtents );
		}

		public Vector3 FindNearestPointTo( Vector3 worldPos, out float normalizedT, float accuracy = 100f, int secondPassIterations = 7, float secondPassExtents = 0.025f )
		{
			Vector3 result = Vector3.zero;
			normalizedT = -1f;

			float step = AccuracyToStepSize( accuracy );

			float minDistance = Mathf.Infinity;
			for( float i = 0f; i < 1f; i += step )
			{
				Vector3 thisPoint = GetPoint( i );
				float thisDistance = ( worldPos - thisPoint ).sqrMagnitude;
				if( thisDistance < minDistance )
				{
					minDistance = thisDistance;
					result = thisPoint;
					normalizedT = i;
				}
			}

			// Do a second pass near the current normalizedT using binary search
			// Credit: https://pomax.github.io/bezierinfo/#projections
			if( secondPassIterations > 0 )
			{
				float minT = normalizedT - secondPassExtents;
				float maxT = normalizedT + secondPassExtents;

				for( int i = 0; i < secondPassIterations; i++ )
				{
					float leftT = ( minT + normalizedT ) * 0.5f;
					float rightT = ( maxT + normalizedT ) * 0.5f;

					Vector3 leftPoint = GetPoint( leftT );
					Vector3 rightPoint = GetPoint( rightT );

					float leftDistance = ( worldPos - leftPoint ).sqrMagnitude;
					float rightDistance = ( worldPos - rightPoint ).sqrMagnitude;

					if( leftDistance < minDistance && leftDistance < rightDistance )
					{
						minDistance = leftDistance;
						result = leftPoint;
						maxT = normalizedT;
						normalizedT = leftT;
					}
					else if( rightDistance < minDistance && rightDistance < leftDistance )
					{
						minDistance = rightDistance;
						result = rightPoint;
						minT = normalizedT;
						normalizedT = rightT;
					}
					else
					{
						minT = leftT;
						maxT = rightT;
					}
				}
			}

			return result;
		}

		public Vector3 FindNearestPointToLine( Vector3 lineStart, Vector3 lineEnd, float accuracy = 100f, int secondPassIterations = 7, float secondPassExtents = 0.025f )
		{
			Vector3 pointOnLine;
			float normalizedT;
			return FindNearestPointToLine( lineStart, lineEnd, out pointOnLine, out normalizedT, accuracy, secondPassIterations, secondPassExtents );
		}

		public Vector3 FindNearestPointToLine( Vector3 lineStart, Vector3 lineEnd, out Vector3 pointOnLine, out float normalizedT, float accuracy = 100f, int secondPassIterations = 7, float secondPassExtents = 0.025f )
		{
			Vector3 result = Vector3.zero;
			pointOnLine = Vector3.zero;
			normalizedT = -1f;

			float step = AccuracyToStepSize( accuracy );

			// Find closest point on line
			// Credit: https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Editor/Mono/HandleUtility.cs#L115-L128
			Vector3 lineDirection = lineEnd - lineStart;
			float length = lineDirection.magnitude;
			Vector3 normalizedLineDirection = lineDirection;
			if( length > .000001f )
				normalizedLineDirection /= length;

			float minDistance = Mathf.Infinity;
			for( float i = 0f; i < 1f; i += step )
			{
				Vector3 thisPoint = GetPoint( i );

				// Find closest point on line
				// Credit: https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Editor/Mono/HandleUtility.cs#L115-L128
				Vector3 closestPointOnLine = lineStart + normalizedLineDirection * Mathf.Clamp( Vector3.Dot( normalizedLineDirection, thisPoint - lineStart ), 0f, length );

				float thisDistance = ( closestPointOnLine - thisPoint ).sqrMagnitude;
				if( thisDistance < minDistance )
				{
					minDistance = thisDistance;
					result = thisPoint;
					pointOnLine = closestPointOnLine;
					normalizedT = i;
				}
			}

			// Do a second pass near the current normalizedT using binary search
			// Credit: https://pomax.github.io/bezierinfo/#projections
			if( secondPassIterations > 0 )
			{
				float minT = normalizedT - secondPassExtents;
				float maxT = normalizedT + secondPassExtents;

				for( int i = 0; i < secondPassIterations; i++ )
				{
					float leftT = ( minT + normalizedT ) * 0.5f;
					float rightT = ( maxT + normalizedT ) * 0.5f;

					Vector3 leftPoint = GetPoint( leftT );
					Vector3 rightPoint = GetPoint( rightT );

					Vector3 leftClosestPointOnLine = lineStart + normalizedLineDirection * Mathf.Clamp( Vector3.Dot( normalizedLineDirection, leftPoint - lineStart ), 0f, length );
					Vector3 rightClosestPointOnLine = lineStart + normalizedLineDirection * Mathf.Clamp( Vector3.Dot( normalizedLineDirection, rightPoint - lineStart ), 0f, length );

					float leftDistance = ( leftClosestPointOnLine - leftPoint ).sqrMagnitude;
					float rightDistance = ( rightClosestPointOnLine - rightPoint ).sqrMagnitude;

					if( leftDistance < minDistance && leftDistance < rightDistance )
					{
						minDistance = leftDistance;
						result = leftPoint;
						pointOnLine = leftClosestPointOnLine;
						maxT = normalizedT;
						normalizedT = leftT;
					}
					else if( rightDistance < minDistance && rightDistance < leftDistance )
					{
						minDistance = rightDistance;
						result = rightPoint;
						pointOnLine = rightClosestPointOnLine;
						minT = normalizedT;
						normalizedT = rightT;
					}
					else
					{
						minT = leftT;
						maxT = rightT;
					}
				}
			}

			return result;
		}

		// Credit: https://gamedev.stackexchange.com/a/27138
		public Vector3 MoveAlongSpline( ref float normalizedT, float deltaMovement, int accuracy = 3 )
		{
			float constant = deltaMovement / ( ( m_loop ? endPoints.Count : ( endPoints.Count - 1 ) ) * accuracy );
			for( int i = 0; i < accuracy; i++ )
				normalizedT += constant / GetTangent( normalizedT ).magnitude;

			return GetPoint( normalizedT );
		}

		public void ConstructLinearPath()
		{
			for( int i = 0; i < endPoints.Count; i++ )
			{
				endPoints[i].handleMode = BezierPoint.HandleMode.Free;
				endPoints[i].RefreshIfChanged();
			}

			for( int i = 0; i < endPoints.Count; i++ )
			{
				if( i < endPoints.Count - 1 )
				{
					Vector3 midPoint = ( endPoints[i].position + endPoints[i + 1].position ) * 0.5f;
					endPoints[i].followingControlPointPosition = midPoint;
					endPoints[i + 1].precedingControlPointPosition = midPoint;
				}
				else
				{
					Vector3 midPoint = ( endPoints[i].position + endPoints[0].position ) * 0.5f;
					endPoints[i].followingControlPointPosition = midPoint;
					endPoints[0].precedingControlPointPosition = midPoint;
				}
			}
		}

		// Credit: http://www.codeproject.com/Articles/31859/Draw-a-Smooth-Curve-through-a-Set-of-2D-Points-wit
		public void AutoConstructSpline()
		{
			for( int i = 0; i < endPoints.Count; i++ )
			{
				endPoints[i].handleMode = BezierPoint.HandleMode.Mirrored;
				endPoints[i].RefreshIfChanged();
			}

			int n = endPoints.Count - 1;
			if( n == 1 )
			{
				endPoints[0].followingControlPointPosition = ( 2 * endPoints[0].position + endPoints[1].position ) / 3f;
				endPoints[1].precedingControlPointPosition = 2 * endPoints[0].followingControlPointPosition - endPoints[0].position;

				return;
			}

			int rhsLength = m_loop ? n + 1 : n;
			if( autoConstructedSplineRhs == null || autoConstructedSplineRhs.Length != rhsLength )
				autoConstructedSplineRhs = new Vector3[rhsLength];
			if( autoConstructedSplineControlPoints == null || rhsLength != autoConstructedSplineControlPoints.Length )
				autoConstructedSplineControlPoints = new Vector3[rhsLength]; // Solution vector
			if( autoConstructedSplineTmp == null || rhsLength != autoConstructedSplineTmp.Length )
				autoConstructedSplineTmp = new float[rhsLength]; // Temp workspace


			for( int i = 1; i < n - 1; i++ )
				autoConstructedSplineRhs[i] = 4 * endPoints[i].position + 2 * endPoints[i + 1].position;

			autoConstructedSplineRhs[0] = endPoints[0].position + 2 * endPoints[1].position;

			if( !m_loop )
				autoConstructedSplineRhs[n - 1] = ( 8 * endPoints[n - 1].position + endPoints[n].position ) * 0.5f;
			else
			{
				autoConstructedSplineRhs[n - 1] = 4 * endPoints[n - 1].position + 2 * endPoints[n].position;
				autoConstructedSplineRhs[n] = ( 8 * endPoints[n].position + endPoints[0].position ) * 0.5f;
			}

			// Get first control points
			float b = 2f;
			autoConstructedSplineControlPoints[0] = autoConstructedSplineRhs[0] / b;
			for( int i = 1; i < rhsLength; i++ ) // Decomposition and forward substitution
			{
				float val = 1f / b;
				autoConstructedSplineTmp[i] = val;
				b = ( i < rhsLength - 1 ? 4f : 3.5f ) - val;
				autoConstructedSplineControlPoints[i] = ( autoConstructedSplineRhs[i] - autoConstructedSplineControlPoints[i - 1] ) / b;
			}

			for( int i = 1; i < rhsLength; i++ )
				autoConstructedSplineControlPoints[rhsLength - i - 1] -= autoConstructedSplineTmp[rhsLength - i] * autoConstructedSplineControlPoints[rhsLength - i]; // Back substitution

			for( int i = 0; i < n; i++ )
			{
				// First control point
				endPoints[i].followingControlPointPosition = autoConstructedSplineControlPoints[i];

				if( m_loop )
					endPoints[i + 1].precedingControlPointPosition = 2 * endPoints[i + 1].position - autoConstructedSplineControlPoints[i + 1];
				else
				{
					// Second control point
					if( i < n - 1 )
						endPoints[i + 1].precedingControlPointPosition = 2 * endPoints[i + 1].position - autoConstructedSplineControlPoints[i + 1];
					else
						endPoints[i + 1].precedingControlPointPosition = ( endPoints[n].position + autoConstructedSplineControlPoints[n - 1] ) * 0.5f;
				}
			}

			if( m_loop )
			{
				float controlPointDistance = Vector3.Distance( endPoints[0].followingControlPointPosition, endPoints[0].position );
				Vector3 direction = Vector3.Normalize( endPoints[n].position - endPoints[1].position );
				endPoints[0].precedingControlPointPosition = endPoints[0].position + direction * controlPointDistance;
				endPoints[0].followingControlPointLocalPosition = -endPoints[0].precedingControlPointLocalPosition;
			}
		}

		// Credit: http://stackoverflow.com/questions/3526940/how-to-create-a-cubic-bezier-curve-when-given-n-points-in-3d
		public void AutoConstructSpline2()
		{
			// This method doesn't work well with 2 end poins
			if( endPoints.Count == 2 )
			{
				AutoConstructSpline();
				return;
			}

			for( int i = 0; i < endPoints.Count; i++ )
			{
				endPoints[i].handleMode = BezierPoint.HandleMode.Mirrored;
				endPoints[i].RefreshIfChanged();
			}

			for( int i = 0; i < endPoints.Count; i++ )
			{
				Vector3 pMinus1, p1, p2;

				if( i == 0 )
				{
					if( m_loop )
						pMinus1 = endPoints[endPoints.Count - 1].position;
					else
						pMinus1 = endPoints[0].position;
				}
				else
					pMinus1 = endPoints[i - 1].position;

				if( m_loop )
				{
					p1 = endPoints[( i + 1 ) % endPoints.Count].position;
					p2 = endPoints[( i + 2 ) % endPoints.Count].position;
				}
				else
				{
					if( i < endPoints.Count - 2 )
					{
						p1 = endPoints[i + 1].position;
						p2 = endPoints[i + 2].position;
					}
					else if( i == endPoints.Count - 2 )
					{
						p1 = endPoints[i + 1].position;
						p2 = endPoints[i + 1].position;
					}
					else
					{
						p1 = endPoints[i].position;
						p2 = endPoints[i].position;
					}
				}

				endPoints[i].followingControlPointPosition = endPoints[i].position + ( p1 - pMinus1 ) / 6f;

				if( i < endPoints.Count - 1 )
					endPoints[i + 1].precedingControlPointPosition = p1 - ( p2 - endPoints[i].position ) / 6f;
				else if( m_loop )
					endPoints[0].precedingControlPointPosition = p1 - ( p2 - endPoints[i].position ) / 6f;
			}
		}

		// Credit: https://stackoverflow.com/a/14241741/2373034
		// Alternative approach: https://stackoverflow.com/a/25458216/2373034
		public void AutoCalculateNormals( float normalAngle = 0f, int smoothness = 10, bool calculateIntermediateNormals = false )
		{
			for( int i = 0; i < endPoints.Count; i++ )
				endPoints[i].RefreshIfChanged();

			Vector3 tangent = new Vector3(), rotatedNormal = new Vector3();
			smoothness = Mathf.Max( 1, smoothness );
			float _1OverSmoothness = 1f / smoothness;

			if( smoothness <= 1 )
				calculateIntermediateNormals = false;

			// Calculate initial point's normal using Frenet formula
			Segment segment = new Segment( endPoints[0], endPoints[1], 0f );
			Vector3 tangent1 = segment.GetTangent( 0f ).normalized;
			Vector3 tangent2 = segment.GetTangent( 0.025f ).normalized;
			Vector3 cross = Vector3.Cross( tangent2, tangent1 ).normalized;
			if( Mathf.Approximately( cross.sqrMagnitude, 0f ) ) // This is not a curved spline but rather a straight line
				cross = Vector3.Cross( tangent2, ( tangent2.x != 0f || tangent2.z != 0f ) ? Vector3.up : Vector3.forward );

			// prevNormal stores the unrotated normal whereas endpoints[index].normal stores the rotated normal
			Vector3 prevNormal = Vector3.Cross( cross, tangent1 ).normalized;
			endPoints[0].normal = Quaternion.AngleAxis( normalAngle + endPoints[0].autoCalculatedNormalAngleOffset, tangent1 ) * prevNormal;

			// Calculate other points' normals by iteratively (smoothness) calculating normals between the previous point and the next point
			for( int i = 0; i < endPoints.Count; i++ )
			{
				if( i < endPoints.Count - 1 )
					segment = new Segment( endPoints[i], endPoints[i + 1], 0f );
				else if( m_loop )
					segment = new Segment( endPoints[i], endPoints[0], 0f );
				else
					break;

				Vector3[] intermediateNormals = null;
				if( !calculateIntermediateNormals )
					segment.point1.intermediateNormals = null;
				else
				{
					intermediateNormals = segment.point1.intermediateNormals;
					if( intermediateNormals == null || intermediateNormals.Length != smoothness + 1 )
						segment.point1.intermediateNormals = intermediateNormals = new Vector3[smoothness + 1];

					intermediateNormals[0] = segment.point1.normal;
				}

				float normalAngle1 = normalAngle + segment.point1.autoCalculatedNormalAngleOffset;
				float normalAngle2 = normalAngle + segment.point2.autoCalculatedNormalAngleOffset;

				for( int j = 1; j <= smoothness; j++ )
				{
					float localT = j * _1OverSmoothness;
					tangent = segment.GetTangent( localT ).normalized;
					prevNormal = Vector3.Cross( tangent, Vector3.Cross( prevNormal, tangent ).normalized ).normalized;

					if( calculateIntermediateNormals )
					{
						float _normalAngle = Mathf.LerpUnclamped( normalAngle1, normalAngle2, localT );
						intermediateNormals[j] = rotatedNormal = ( _normalAngle == 0f ) ? prevNormal : ( Quaternion.AngleAxis( _normalAngle, tangent ) * prevNormal );
					}
				}

				if( !calculateIntermediateNormals )
					rotatedNormal = ( normalAngle2 == 0f ) ? prevNormal : ( Quaternion.AngleAxis( normalAngle2, tangent ) * prevNormal );

				if( i < endPoints.Count - 1 )
					endPoints[i + 1].normal = rotatedNormal;
				else
				{
					if( !calculateIntermediateNormals )
					{
						if( rotatedNormal != -endPoints[0].normal )
							endPoints[0].normal = ( endPoints[0].normal + rotatedNormal ).normalized;
					}
					else
					{
						// In a looping spline, the first end point's normal value is a special case because the initial value that we've assigned to it
						// might end up vastly different from the final rotatedNormal that we've found. To accommodate to this change, we'll find the
						// angle difference between these two values and gradually apply that difference to the first end point's intermediate normals
						Vector3 initialNormal0 = endPoints[0].normal;
						float normal0DeltaAngle = Vector3.Angle( initialNormal0, rotatedNormal );
						if( Mathf.Abs( normal0DeltaAngle ) > 5f )
						{
							// Vector3.SignedAngle: https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Runtime/Export/Math/Vector3.cs#L316-L328
							// The function itself isn't available on Unity 5.6 so its source code is copy&pasted here
							float cross_x = initialNormal0.y * rotatedNormal.z - initialNormal0.z * rotatedNormal.y;
							float cross_y = initialNormal0.z * rotatedNormal.x - initialNormal0.x * rotatedNormal.z;
							float cross_z = initialNormal0.x * rotatedNormal.y - initialNormal0.y * rotatedNormal.x;
							normal0DeltaAngle *= Mathf.Sign( tangent.x * cross_x + tangent.y * cross_y + tangent.z * cross_z );

							segment = new Segment( endPoints[0], endPoints[1], 0f );
							intermediateNormals = endPoints[0].intermediateNormals;
							endPoints[0].normal = intermediateNormals[0] = rotatedNormal;

							for( int j = 1; j < smoothness; j++ )
							{
								float localT = j * _1OverSmoothness;
								intermediateNormals[j] = Quaternion.AngleAxis( Mathf.LerpUnclamped( normal0DeltaAngle, 0f, localT ), segment.GetTangent( localT ).normalized ) * intermediateNormals[j];
							}
						}
					}
				}
			}
		}

		// Credit: https://www.youtube.com/watch?v=d9k97JemYbM
		public EvenlySpacedPointsHolder CalculateEvenlySpacedPoints( float resolution = 10f, float accuracy = 3f )
		{
			int segmentCount = m_loop ? endPoints.Count : ( endPoints.Count - 1 );
			List<float> evenlySpacedPoints = new List<float>( segmentCount + Mathf.CeilToInt( segmentCount * resolution * 1.25f ) );

			// Calculate each spline segment's approximate length and store it temporarily in the list so that
			// we won't have to calculate the same value twice in the 2nd loop. We'll remove these length values
			// from the list at the end of the operation
			float estimatedSplineLength = 0f;
			for( int i = 0; i < segmentCount; i++ )
			{
				BezierPoint point1 = endPoints[i];
				BezierPoint point2 = ( i < endPoints.Count - 1 ) ? endPoints[i + 1] : endPoints[0];

				float controlNetLength = Vector3.Distance( point1.position, point1.followingControlPointPosition ) + Vector3.Distance( point1.followingControlPointPosition, point2.precedingControlPointPosition ) + Vector3.Distance( point2.precedingControlPointPosition, point2.position );
				float estimatedCurveLength = Vector3.Distance( point1.position, point2.position ) + controlNetLength * 0.5f;

				estimatedSplineLength += estimatedCurveLength;
				evenlySpacedPoints.Add( estimatedCurveLength );
			}

			float averageSegmentLength = estimatedSplineLength / segmentCount;
			float distanceBetweenEvenlySpacedPoints = averageSegmentLength / resolution;
			float remainingDistanceToEvenlySpacedPoint = distanceBetweenEvenlySpacedPoints;
			float totalLength = 0f;

			Vector3 previousPoint = endPoints[0].position;
			evenlySpacedPoints.Add( 0f );

			for( int i = 0; i < segmentCount; i++ )
			{
				Segment segment = new Segment( endPoints[i], ( i < endPoints.Count - 1 ) ? endPoints[i + 1] : endPoints[0], 0f );

				float estimatedCurveLength = evenlySpacedPoints[i];
				float tMultiplier = 1f / ( resolution * accuracy * ( estimatedCurveLength / averageSegmentLength ) );
				float t = 0, previousT = 0f;
				while( t < 1f )
				{
					t += tMultiplier;
					if( t > 1f )
						t = 1f;

					Vector3 point = segment.GetPoint( t );
					float distanceToPreviousPoint = Vector3.Distance( previousPoint, point );
					while( distanceToPreviousPoint >= remainingDistanceToEvenlySpacedPoint )
					{
						float newEvenlySpacedPointLocalT = previousT + ( t - previousT ) * ( remainingDistanceToEvenlySpacedPoint / distanceToPreviousPoint );
						evenlySpacedPoints.Add( segment.GetNormalizedT( newEvenlySpacedPointLocalT ) );

						//distanceToPreviousPoint -= remainingDistanceToEvenlySpacedPoint;
						distanceToPreviousPoint = Vector3.Distance( segment.GetPoint( newEvenlySpacedPointLocalT ), point );
						remainingDistanceToEvenlySpacedPoint = distanceBetweenEvenlySpacedPoints;
						totalLength += distanceBetweenEvenlySpacedPoints;
						previousT = newEvenlySpacedPointLocalT;
					}

					remainingDistanceToEvenlySpacedPoint -= distanceToPreviousPoint;
					previousT = t;
					previousPoint = point;
				}
			}

			totalLength += distanceBetweenEvenlySpacedPoints - remainingDistanceToEvenlySpacedPoint;

			// If the last calculated evenly spaced point is too close to the final point (t=1f), remove it.
			// The space between last 3 evenly spaced points won't really be even but the difference will be
			// negligible when resolution isn't too small
			if( remainingDistanceToEvenlySpacedPoint >= distanceBetweenEvenlySpacedPoints * 0.5f )
				evenlySpacedPoints.RemoveAt( evenlySpacedPoints.Count - 1 );

			evenlySpacedPoints.Add( 1f );

			// Remove spline segment lengths from list (which were temporarily stored there)
			evenlySpacedPoints.RemoveRange( 0, segmentCount );

			return new EvenlySpacedPointsHolder( this, totalLength, evenlySpacedPoints.ToArray() );
		}

		public PointCache GeneratePointCache( PointCacheFlags cachedData = PointCacheFlags.All, int resolution = 100 )
		{
			return GeneratePointCache( evenlySpacedPoints, defaultExtraDataLerpFunction, cachedData, resolution );
		}

		public PointCache GeneratePointCache( EvenlySpacedPointsHolder lookupTable, ExtraDataLerpFunction extraDataLerpFunction, PointCacheFlags cachedData = PointCacheFlags.All, int resolution = 100 )
		{
			if( cachedData == PointCacheFlags.None )
				return new PointCache( null, null, null, null, null, false );

			if( lookupTable == null )
				lookupTable = evenlySpacedPoints;

			if( resolution < 2 )
				resolution = 2;

			Vector3[] positions = null, normals = null, tangents = null, bitangents = null;
			BezierPoint.ExtraData[] extraDatas = null;
			if( ( cachedData & PointCacheFlags.Positions ) == PointCacheFlags.Positions )
				positions = new Vector3[resolution];
			if( ( cachedData & PointCacheFlags.Normals ) == PointCacheFlags.Normals )
				normals = new Vector3[resolution];
			if( ( cachedData & PointCacheFlags.Tangents ) == PointCacheFlags.Tangents )
				tangents = new Vector3[resolution];
			if( ( cachedData & PointCacheFlags.Bitangents ) == PointCacheFlags.Bitangents )
				bitangents = new Vector3[resolution];
			if( ( cachedData & PointCacheFlags.ExtraDatas ) == PointCacheFlags.ExtraDatas )
				extraDatas = new BezierPoint.ExtraData[resolution];

			float indexMultiplier = 1f / ( resolution - 1 );

			for( int i = 0; i < resolution; i++ )
			{
				Segment segment = GetSegmentAt( lookupTable.GetNormalizedTAtPercentage( i * indexMultiplier ) );

				if( positions != null )
					positions[i] = segment.GetPoint();
				if( normals != null )
					normals[i] = segment.GetNormal().normalized;
				if( tangents != null )
					tangents[i] = segment.GetTangent().normalized;
				if( bitangents != null )
				{
					Vector3 normal = ( normals != null ) ? normals[i] : segment.GetNormal().normalized;
					Vector3 tangent = ( tangents != null ) ? tangents[i] : segment.GetTangent().normalized;
					bitangents[i] = Vector3.Cross( normal, tangent );
				}
				if( extraDatas != null )
					extraDatas[i] = segment.GetExtraData( extraDataLerpFunction );
			}

			return new PointCache( positions, normals, tangents, bitangents, extraDatas, loop );
		}

		public void ClearIntermediateNormals()
		{
			for( int i = 0; i < endPoints.Count; i++ )
				endPoints[i].intermediateNormals = null;
		}

		private float AccuracyToStepSize( float accuracy )
		{
			if( accuracy <= 0f )
				return 0.2f;

			return Mathf.Clamp( 1f / accuracy, 0.001f, 0.2f );
		}

		// Renders the spline gizmo during gameplay
		// Credit: https://docs.unity3d.com/ScriptReference/GL.html
		private void OnRenderObject()
		{
			if( !drawGizmos || endPoints.Count < 2 )
				return;

			if( !gizmoMaterial )
			{
				Shader shader = Shader.Find( "Hidden/Internal-Colored" );
				gizmoMaterial = new Material( shader ) { hideFlags = HideFlags.HideAndDontSave };
				gizmoMaterial.SetInt( "_SrcBlend", (int) UnityEngine.Rendering.BlendMode.SrcAlpha );
				gizmoMaterial.SetInt( "_DstBlend", (int) UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha );
				gizmoMaterial.SetInt( "_Cull", (int) UnityEngine.Rendering.CullMode.Off );
				gizmoMaterial.SetInt( "_ZWrite", 0 );
			}

			gizmoMaterial.SetPass( 0 );

			GL.Begin( GL.LINES );
			GL.Color( gizmoColor );

			Vector3 lastPos = endPoints[0].position;

			float gizmoStep = 1f / ( endPoints.Count * Mathf.Clamp( gizmoSmoothness, 1, 30 ) );
			for( float i = gizmoStep; i < 1f; i += gizmoStep )
			{
				GL.Vertex3( lastPos.x, lastPos.y, lastPos.z );
				lastPos = GetPoint( i );
				GL.Vertex3( lastPos.x, lastPos.y, lastPos.z );
			}

			GL.Vertex3( lastPos.x, lastPos.y, lastPos.z );
			lastPos = GetPoint( 1f );
			GL.Vertex3( lastPos.x, lastPos.y, lastPos.z );

			GL.End();
		}

		IEnumerator<BezierPoint> IEnumerable<BezierPoint>.GetEnumerator()
		{
			return endPoints.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return endPoints.GetEnumerator();
		}

#if UNITY_EDITOR
		[ContextMenu( "Invert Spline" )]
		private void InvertSplineContextMenu()
		{
			InvertSpline( "Invert spline" );
		}

		internal void Reset()
		{
			for( int i = endPoints.Count - 1; i >= 0; i-- )
				UnityEditor.Undo.DestroyObjectImmediate( endPoints[i].gameObject );

			Initialize( 2 );

			endPoints[0].localPosition = Vector3.back;
			endPoints[1].localPosition = Vector3.forward;

			UnityEditor.Undo.RegisterCreatedObjectUndo( endPoints[0].gameObject, "Initialize Spline" );
			UnityEditor.Undo.RegisterCreatedObjectUndo( endPoints[1].gameObject, "Initialize Spline" );

			UnityEditor.Selection.activeTransform = endPoints[0].transform;
		}
#endif
	}
}