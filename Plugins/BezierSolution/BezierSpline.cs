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
#if UNITY_EDITOR
	internal enum SplineAutoConstructMode { None = 0, Linear = 1, Smooth1 = 2, Smooth2 = 3 };
#endif

	[ExecuteInEditMode]
	public class BezierSpline : MonoBehaviour, IEnumerable<BezierPoint>
	{
		public struct PointIndexTuple
		{
			public readonly BezierPoint point1, point2;
			public readonly int index1, index2;
			public readonly float t;

			public PointIndexTuple( BezierSpline spline, int index1, int index2, float t )
			{
				this.point1 = spline[index1];
				this.point2 = spline[index2];
				this.index1 = index1;
				this.index2 = index2;
				this.t = t;
			}

			public Vector3 GetPoint()
			{
				return GetPoint( t );
			}

			public Vector3 GetPoint( float localT )
			{
				float oneMinusLocalT = 1f - localT;

				return oneMinusLocalT * oneMinusLocalT * oneMinusLocalT * point1.position +
					   3f * oneMinusLocalT * oneMinusLocalT * localT * point1.followingControlPointPosition +
					   3f * oneMinusLocalT * localT * localT * point2.precedingControlPointPosition +
					   localT * localT * localT * point2.position;
			}

			public Vector3 GetTangent()
			{
				return GetTangent( t );
			}

			public Vector3 GetTangent( float localT )
			{
				float oneMinusLocalT = 1f - localT;

				return 3f * oneMinusLocalT * oneMinusLocalT * ( point1.followingControlPointPosition - point1.position ) +
					   6f * oneMinusLocalT * localT * ( point2.precedingControlPointPosition - point1.followingControlPointPosition ) +
					   3f * localT * localT * ( point2.position - point2.precedingControlPointPosition );
			}

			public Vector3 GetNormal()
			{
				return GetNormal( t );
			}

			public Vector3 GetNormal( float localT )
			{
				Vector3 startNormal = point1.normal;
				Vector3 endNormal = point2.normal;

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

			public BezierPoint.ExtraData GetExtraData()
			{
				return defaultExtraDataLerpFunction( point1.extraData, point2.extraData, t );
			}

			public BezierPoint.ExtraData GetExtraData( float localT )
			{
				return defaultExtraDataLerpFunction( point1.extraData, point2.extraData, localT );
			}

			public BezierPoint.ExtraData GetExtraData( ExtraDataLerpFunction lerpFunction )
			{
				return lerpFunction( point1.extraData, point2.extraData, t );
			}

			public BezierPoint.ExtraData GetExtraData( float localT, ExtraDataLerpFunction lerpFunction )
			{
				return lerpFunction( point1.extraData, point2.extraData, localT );
			}
		}

		public delegate BezierPoint.ExtraData ExtraDataLerpFunction( BezierPoint.ExtraData data1, BezierPoint.ExtraData data2, float normalizedT );

		private static readonly ExtraDataLerpFunction defaultExtraDataLerpFunction = BezierPoint.ExtraData.LerpUnclamped;
		private static Material gizmoMaterial;

		private List<BezierPoint> endPoints = new List<BezierPoint>(); // This is not readonly because otherwise BezierWalkers' "Simulate In Editor" functionality may break after recompilation

		public bool loop = false;
		public bool drawGizmos = false;

		public Color gizmoColor = Color.white;
		private float gizmoStep = 0.05f;
		[SerializeField] private int m_gizmoSmoothness = 4;
		public int gizmoSmoothness
		{
			get { return m_gizmoSmoothness; }
			set
			{
				m_gizmoSmoothness = value;
				gizmoStep = 1f / ( endPoints.Count * Mathf.Clamp( m_gizmoSmoothness, 1, 30 ) );
			}
		}

#if UNITY_EDITOR
		internal BezierPoint[] Internal_Points { get { return endPoints.ToArray(); } }

		[System.NonSerialized]
		internal bool Internal_IsDirty;

		[SerializeField]
		[HideInInspector]
		internal SplineAutoConstructMode Internal_AutoConstructMode = SplineAutoConstructMode.None;

		[SerializeField]
		[HideInInspector]
		internal bool Internal_AutoCalculateNormals = false;

		[SerializeField]
		[HideInInspector]
		internal bool Internal_FlipAutoCalculatedNormals = false;
#endif

		public int Count { get { return endPoints.Count; } }
		public BezierPoint this[int index] { get { return endPoints[index]; } }

		public float Length { get { return GetLengthApproximately( 0f, 1f ); } }

		private void Awake()
		{
			Refresh();
		}

#if UNITY_EDITOR
		private void OnTransformChildrenChanged()
		{
			Refresh();
		}

		private void LateUpdate()
		{
			Internal_CheckDirty();
		}

		internal void Internal_CheckDirty()
		{
			if( Internal_IsDirty && endPoints.Count >= 2 )
			{
				switch( Internal_AutoConstructMode )
				{
					case SplineAutoConstructMode.Linear: ConstructLinearPath(); break;
					case SplineAutoConstructMode.Smooth1: AutoConstructSpline(); break;
					case SplineAutoConstructMode.Smooth2: AutoConstructSpline2(); break;
				}

				if( Internal_AutoCalculateNormals )
					AutoCalculateNormals( Internal_FlipAutoCalculatedNormals );

				Internal_IsDirty = false;
			}
		}

		internal void Internal_SetDirtyImmediatelyWithUndo( string undo )
		{
			if( Internal_AutoCalculateNormals || Internal_AutoConstructMode != SplineAutoConstructMode.None )
			{
				for( int i = 0; i < endPoints.Count; i++ )
				{
					UnityEditor.Undo.RecordObject( endPoints[i], undo );
					UnityEditor.Undo.RecordObject( endPoints[i].transform, undo );
				}

				Internal_IsDirty = true;
				Internal_CheckDirty();
			}
		}
#endif

		public void Initialize( int endPointsCount )
		{
			if( endPointsCount < 2 )
			{
				Debug.LogError( "Can't initialize spline with " + endPointsCount + " point(s). At least 2 points are needed" );
				return;
			}

			Refresh();

			for( int i = endPoints.Count - 1; i >= 0; i-- )
				DestroyImmediate( endPoints[i].gameObject );

			endPoints.Clear();

			for( int i = 0; i < endPointsCount; i++ )
				InsertNewPointAt( i );

			Refresh();
		}

		public void Refresh()
		{
			endPoints.Clear();
			GetComponentsInChildren( endPoints );

			gizmoSmoothness = gizmoSmoothness; // Recalculate gizmoStep

#if UNITY_EDITOR
			for( int i = 0; i < endPoints.Count; i++ )
			{
				endPoints[i].Internal_Spline = this;
				endPoints[i].Internal_Index = i;
			}

			Internal_IsDirty = true;
#endif
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

			Transform parent = endPoints.Count == 0 ? transform : ( index == 0 ? endPoints[0].transform.parent : endPoints[index - 1].transform.parent );
			int siblingIndex = index == 0 ? 0 : endPoints[index - 1].transform.GetSiblingIndex() + 1;
			point.transform.SetParent( parent, false );
			point.transform.SetSiblingIndex( siblingIndex );

			if( endPoints.Count == prevCount ) // If spline isn't automatically Refresh()'ed
				endPoints.Insert( index, point );

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

			DestroyImmediate( point.gameObject );
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

			if( point1Parent != point2Parent )
			{
				point1.transform.SetParent( point2Parent, true );
				point2.transform.SetParent( point1Parent, true );
			}

			point1.transform.SetSiblingIndex( point2SiblingIndex );
			point2.transform.SetSiblingIndex( point1SiblingIndex );

#if UNITY_EDITOR
			Refresh();
#endif
		}

		public void ChangePointIndex( int previousIndex, int newIndex )
		{
			Internal_ChangePointIndex( previousIndex, newIndex, null );
		}

		internal void Internal_ChangePointIndex( int previousIndex, int newIndex, string undo )
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

#if UNITY_EDITOR
			Refresh();
#endif
		}

		public int IndexOf( BezierPoint point )
		{
			return endPoints.IndexOf( point );
		}

		public Vector3 GetPoint( float normalizedT )
		{
			if( !loop )
			{
				if( normalizedT <= 0f )
					return endPoints[0].position;
				else if( normalizedT >= 1f )
					return endPoints[endPoints.Count - 1].position;
			}
			else
			{
				if( normalizedT < 0f )
					normalizedT += 1f;
				else if( normalizedT >= 1f )
					normalizedT -= 1f;
			}

			float t = normalizedT * ( loop ? endPoints.Count : ( endPoints.Count - 1 ) );

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
			if( !loop )
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
				if( normalizedT < 0f )
					normalizedT += 1f;
				else if( normalizedT >= 1f )
					normalizedT -= 1f;
			}

			float t = normalizedT * ( loop ? endPoints.Count : ( endPoints.Count - 1 ) );

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
			if( !loop )
			{
				if( normalizedT <= 0f )
					return endPoints[0].normal;
				else if( normalizedT >= 1f )
					return endPoints[endPoints.Count - 1].normal;
			}
			else
			{
				if( normalizedT < 0f )
					normalizedT += 1f;
				else if( normalizedT >= 1f )
					normalizedT -= 1f;
			}

			float t = normalizedT * ( loop ? endPoints.Count : ( endPoints.Count - 1 ) );

			int startIndex = (int) t;
			int endIndex = startIndex + 1;

			if( endIndex == endPoints.Count )
				endIndex = 0;

			Vector3 startNormal = endPoints[startIndex].normal;
			Vector3 endNormal = endPoints[endIndex].normal;

			float localT = t - startIndex;

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
			if( !loop )
			{
				if( normalizedT <= 0f )
					return endPoints[0].extraData;
				else if( normalizedT >= 1f )
					return endPoints[endPoints.Count - 1].extraData;
			}
			else
			{
				if( normalizedT < 0f )
					normalizedT += 1f;
				else if( normalizedT >= 1f )
					normalizedT -= 1f;
			}

			float t = normalizedT * ( loop ? endPoints.Count : ( endPoints.Count - 1 ) );

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
			Vector3 lastPoint = GetPoint( startNormalizedT );
			for( float i = startNormalizedT + step; i < endNormalizedT; i += step )
			{
				Vector3 thisPoint = GetPoint( i );
				length += Vector3.Distance( thisPoint, lastPoint );
				lastPoint = thisPoint;
			}

			length += Vector3.Distance( lastPoint, GetPoint( endNormalizedT ) );

			return length;
		}

		public PointIndexTuple GetNearestPointIndicesTo( float normalizedT )
		{
			if( !loop )
			{
				if( normalizedT <= 0f )
					return new PointIndexTuple( this, 0, 1, 0f );
				else if( normalizedT >= 1f )
					return new PointIndexTuple( this, endPoints.Count - 1, endPoints.Count - 1, 1f );
			}
			else
			{
				if( normalizedT < 0f )
					normalizedT += 1f;
				else if( normalizedT >= 1f )
					normalizedT -= 1f;
			}

			float t = normalizedT * ( loop ? endPoints.Count : ( endPoints.Count - 1 ) );

			int startIndex = (int) t;
			int endIndex = startIndex + 1;

			if( endIndex == endPoints.Count )
				endIndex = 0;

			return new PointIndexTuple( this, startIndex, endIndex, t - startIndex );
		}

		public Vector3 FindNearestPointTo( Vector3 worldPos, float accuracy = 100f )
		{
			float normalizedT;
			return FindNearestPointTo( worldPos, out normalizedT, accuracy );
		}

		public Vector3 FindNearestPointTo( Vector3 worldPos, out float normalizedT, float accuracy = 100f )
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

			return result;
		}

		// Credit: https://gamedev.stackexchange.com/a/27138
		public Vector3 MoveAlongSpline( ref float normalizedT, float deltaMovement, int accuracy = 3 )
		{
			float constant = deltaMovement / ( ( loop ? endPoints.Count : ( endPoints.Count - 1 ) ) * accuracy );
			for( int i = 0; i < accuracy; i++ )
				normalizedT += constant / GetTangent( normalizedT ).magnitude;

			return GetPoint( normalizedT );
		}

		public void ConstructLinearPath()
		{
			for( int i = 0; i < endPoints.Count; i++ )
			{
				endPoints[i].handleMode = BezierPoint.HandleMode.Free;

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
				endPoints[i].handleMode = BezierPoint.HandleMode.Mirrored;

			int n = endPoints.Count - 1;
			if( n == 1 )
			{
				endPoints[0].followingControlPointPosition = ( 2 * endPoints[0].position + endPoints[1].position ) / 3f;
				endPoints[1].precedingControlPointPosition = 2 * endPoints[0].followingControlPointPosition - endPoints[0].position;

				return;
			}

			Vector3[] rhs;
			if( loop )
				rhs = new Vector3[n + 1];
			else
				rhs = new Vector3[n];

			for( int i = 1; i < n - 1; i++ )
				rhs[i] = 4 * endPoints[i].position + 2 * endPoints[i + 1].position;

			rhs[0] = endPoints[0].position + 2 * endPoints[1].position;

			if( !loop )
				rhs[n - 1] = ( 8 * endPoints[n - 1].position + endPoints[n].position ) * 0.5f;
			else
			{
				rhs[n - 1] = 4 * endPoints[n - 1].position + 2 * endPoints[n].position;
				rhs[n] = ( 8 * endPoints[n].position + endPoints[0].position ) * 0.5f;
			}

			// Get first control points
			int rhsLength = rhs.Length;
			Vector3[] controlPoints = new Vector3[rhsLength]; // Solution vector
			float[] tmp = new float[rhsLength]; // Temp workspace

			float b = 2f;
			controlPoints[0] = rhs[0] / b;
			for( int i = 1; i < rhsLength; i++ ) // Decomposition and forward substitution
			{
				float val = 1f / b;
				tmp[i] = val;
				b = ( i < rhsLength - 1 ? 4f : 3.5f ) - val;
				controlPoints[i] = ( rhs[i] - controlPoints[i - 1] ) / b;
			}

			for( int i = 1; i < rhsLength; i++ )
				controlPoints[rhsLength - i - 1] -= tmp[rhsLength - i] * controlPoints[rhsLength - i]; // Backsubstitution

			for( int i = 0; i < n; i++ )
			{
				// First control point
				endPoints[i].followingControlPointPosition = controlPoints[i];

				if( loop )
					endPoints[i + 1].precedingControlPointPosition = 2 * endPoints[i + 1].position - controlPoints[i + 1];
				else
				{
					// Second control point
					if( i < n - 1 )
						endPoints[i + 1].precedingControlPointPosition = 2 * endPoints[i + 1].position - controlPoints[i + 1];
					else
						endPoints[i + 1].precedingControlPointPosition = ( endPoints[n].position + controlPoints[n - 1] ) * 0.5f;
				}
			}

			if( loop )
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
				Vector3 pMinus1, p1, p2;

				if( i == 0 )
				{
					if( loop )
						pMinus1 = endPoints[endPoints.Count - 1].position;
					else
						pMinus1 = endPoints[0].position;
				}
				else
					pMinus1 = endPoints[i - 1].position;

				if( loop )
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
				endPoints[i].handleMode = BezierPoint.HandleMode.Mirrored;

				if( i < endPoints.Count - 1 )
					endPoints[i + 1].precedingControlPointPosition = p1 - ( p2 - endPoints[i].position ) / 6f;
				else if( loop )
					endPoints[0].precedingControlPointPosition = p1 - ( p2 - endPoints[i].position ) / 6f;
			}
		}

		// Alternative approach1: https://stackoverflow.com/a/25458216/2373034
		// Alternative approach2: https://stackoverflow.com/a/14241741/2373034
		public void AutoCalculateNormals( bool flipNormals )
		{
			const float DELTA_T = 0.025f;
			const float ONE_MINUS_DELTA_T = 1f - DELTA_T;

			for( int i = 0; i < endPoints.Count; i++ )
			{
				if( i < endPoints.Count - 1 )
					endPoints[i].normal = CalculateFrenetNormal( i, i + 1, 0f, DELTA_T, endPoints[i].autoCalculatedNormalAngleOffset );
				else if( loop )
					endPoints[i].normal = CalculateFrenetNormal( i, 0, 0f, DELTA_T, endPoints[i].autoCalculatedNormalAngleOffset );
				else
					endPoints[i].normal = CalculateFrenetNormal( i - 1, i, ONE_MINUS_DELTA_T, 1f, endPoints[i].autoCalculatedNormalAngleOffset );

				if( flipNormals )
					endPoints[i].normal = -endPoints[i].normal;
			}

			if( loop )
			{
				Vector3 point0Normal2 = CalculateFrenetNormal( endPoints.Count - 1, 0, ONE_MINUS_DELTA_T, 1f, endPoints[0].autoCalculatedNormalAngleOffset );
				if( flipNormals )
					point0Normal2 = -point0Normal2;

				if( point0Normal2 != -endPoints[0].normal )
					endPoints[0].normal = ( endPoints[0].normal + point0Normal2 ).normalized;
			}
		}

		private Vector3 CalculateFrenetNormal( int pointIndex1, int pointIndex2, float localT1, float localT2, float rotateAngle )
		{
			PointIndexTuple tuple = new PointIndexTuple( this, pointIndex1, pointIndex2, 0f );
			Vector3 tangent1 = tuple.GetTangent( localT1 ).normalized;
			Vector3 tangent2 = tuple.GetTangent( localT2 ).normalized;
			Vector3 cross = Vector3.Cross( tangent2, tangent1 ).normalized;
			if( Mathf.Approximately( cross.sqrMagnitude, 0f ) ) // This is not a curved spline but rather a straight line
				cross = Vector3.Cross( tangent2, ( tangent2.x != 0f || tangent2.z != 0f ) ? Vector3.up : Vector3.forward );

			Vector3 normal = Vector3.Cross( cross, tangent1 ).normalized;
			if( rotateAngle != 0f )
				normal = Quaternion.AngleAxis( rotateAngle, tangent1 ) * normal;

			return normal;
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
		public void Reset()
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