using UnityEngine;

#if UNITY_EDITOR
[System.Serializable]
public struct EndPointIndex
{
	public const int CONTROL_POINT_NONE = 0;
	public const int CONTROL_POINT_PRECEDING = 1;
	public const int CONTROL_POINT_FOLLOWING = 2;

	public int index;
	public int controlPointIndex;

	public EndPointIndex( int index, int controlPointIndex )
	{
		this.index = index;
		this.controlPointIndex = controlPointIndex;
	}

	public static implicit operator EndPointIndex( int index )
	{
		return new EndPointIndex( index, CONTROL_POINT_NONE );
	}

	public static implicit operator int( EndPointIndex endPointIndex )
	{
		return endPointIndex.index;
	}
}
#endif

[ExecuteInEditMode]
public class BezierSpline : MonoBehaviour
{
	[SerializeField]
	private BezierPoint[] endPoints;

	#if UNITY_EDITOR
	[HideInInspector]
	public EndPointIndex selectedEndPointIndex = -1;
	#endif

	private Transform cachedTransform;
	
	public bool loop = false;

	public int Count { get { return endPoints.Length; } }

	public float Length
	{
		get
		{
			return GetLengthApproximately( 0f, 1f );
		}
	}
	
	private Matrix4x4 m_localToWorldMatrix;
	public Matrix4x4 localToWorldMatrix { get { return m_localToWorldMatrix; } }
	
	private Matrix4x4 m_worldToLocalMatrix;
	public Matrix4x4 worldToLocalMatrix { get { return m_worldToLocalMatrix; } }

	public BezierPoint this[int index]
	{
		get
		{
			if( index < Count )
				return endPoints[index];

			Debug.LogError( "Bezier index " + index + " is out of range: " + Count );
			return null;
		}
	}

	void Awake()
	{
		cachedTransform = transform;

		m_localToWorldMatrix = cachedTransform.localToWorldMatrix;
		m_worldToLocalMatrix = cachedTransform.worldToLocalMatrix;
	}

	void Update()
	{
		#if UNITY_EDITOR
		cachedTransform = transform;
		#endif

		if( cachedTransform.hasChanged )
		{
			Revalidate();
			cachedTransform.hasChanged = false;
		}
	}

	public void Initialize( BezierPoint[] points )
	{
		if( points == null )
		{
			Debug.LogError( "Initialize spline with a valid array of points!" );
			return;
		}

		if( points.Length < 2 )
		{
			Debug.LogError( "Can't initialize spline with " + points.Length + " point(s). At least 2 points are needed" );
			return;
		}

		endPoints = points;
	}

	public void Initialize( Vector3[] localPositions )
	{
		if( localPositions == null )
		{
			Debug.LogError( "Initialize spline with a valid array of points!" );
			return;
		}

		if( localPositions.Length < 2 )
		{
			Debug.LogError( "Can't initialize spline with " + localPositions.Length + " point(s). At least 2 points are needed" );
			return;
		}

		endPoints = new BezierPoint[localPositions.Length];

		for( int i = 0; i < localPositions.Length; i++ )
		{
			endPoints[i] = new BezierPoint( this, localPositions[i] );
		}
	}
	
	public BezierPoint InsertNewPointAt( int index )
	{
		if( index < 0 || index > endPoints.Length )
		{
			Debug.LogError( "Index " + index + " is out of range: [0," + endPoints.Length + "]" );
			return null;
		}

		Vector3 localPosition;
		if( index > 0 && index < endPoints.Length )
		{
			localPosition = ( endPoints[index - 1].localPosition + endPoints[index].localPosition ) * 0.5f;
		}
		else if( index == 0 )
		{
			if( loop )
				localPosition = ( endPoints[0].localPosition + endPoints[endPoints.Length - 1].localPosition ) * 0.5f;
			else
				localPosition = endPoints[0].localPosition - ( endPoints[1].localPosition - endPoints[0].localPosition ) * 0.5f;
		}
		else
		{
			if( loop )
				localPosition = ( endPoints[0].localPosition + endPoints[endPoints.Length - 1].localPosition ) * 0.5f;
			else
				localPosition = endPoints[index - 1].localPosition + ( endPoints[index - 1].localPosition - endPoints[index - 2].localPosition ) * 0.5f;
		}
		
		return InsertNewPointAt( index, new BezierPoint( this, localPosition ) );
	}

	public BezierPoint InsertNewPointAt( int index, Vector3 localPosition )
	{
		return InsertNewPointAt( index, new BezierPoint( this, localPosition ) );
	}

	public BezierPoint InsertNewPointAt( int index, BezierPoint point )
	{
		if( index < 0 || index > endPoints.Length )
		{
			Debug.LogError( "Index " + index + " is out of range: [0," + endPoints.Length + "]" );
			return null;
		}

		BezierPoint[] newEndPoints = new BezierPoint[endPoints.Length + 1];

		for( int i = 0; i < index; i++ )
		{
			newEndPoints[i] = endPoints[i];
		}

		newEndPoints[index] = point;

		for( int i = index; i < endPoints.Length; i++ )
		{
			newEndPoints[i + 1] = endPoints[i];
		}

		endPoints = newEndPoints;

		return point;
	}

	public void RemovePointAt( int index )
	{
		if( endPoints.Length <= 2 )
		{
			Debug.LogError( "Can't remove point: spline must consist of at least two points!" );
			return;
		}

		if( index < 0 || index >= endPoints.Length )
		{
			Debug.LogError( "Index " + index + " is out of range: [0," + endPoints.Length + ")" );
			return;
		}

		BezierPoint[] newEndPoints = new BezierPoint[endPoints.Length - 1];

		for( int i = 0; i < index; i++ )
		{
			newEndPoints[i] = endPoints[i];
		}

		for( int i = index; i < endPoints.Length - 1; i++ )
		{
			newEndPoints[i] = endPoints[i + 1];
		}

		endPoints = newEndPoints;
	}

	public Vector3 GetPoint( float normalizedT )
	{
		if( normalizedT <= 0f )
			return endPoints[0].position;
		else if( normalizedT >= 1f )
		{
			if( loop )
				return endPoints[0].position;

			return endPoints[endPoints.Length - 1].position;
		}

		float t = normalizedT * ( loop ? endPoints.Length : ( endPoints.Length - 1 ) );

		BezierPoint startPoint, endPoint;

		int startIndex = (int) t;
		int endIndex = startIndex + 1;

		if( endIndex == endPoints.Length )
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
		if( normalizedT <= 0f )
			return 3f * ( endPoints[0].followingControlPointPosition - endPoints[0].position );
		else if( normalizedT >= 1f )
		{
			if( loop )
				return 3f * ( endPoints[0].position - endPoints[0].precedingControlPointPosition );
			else
			{
				int index = endPoints.Length - 1;
				return 3f * ( endPoints[index].position - endPoints[index].precedingControlPointPosition );
			}
		}

		float t = normalizedT * ( loop ? endPoints.Length : ( endPoints.Length - 1 ) );

		BezierPoint startPoint, endPoint;

		int startIndex = (int) t;
		int endIndex = startIndex + 1;

		if( endIndex == endPoints.Length )
			endIndex = 0;

		startPoint = endPoints[startIndex];
		endPoint = endPoints[endIndex];

		float localT = t - startIndex;
		float oneMinusLocalT = 1f - localT;

		return 3f * oneMinusLocalT * oneMinusLocalT * ( startPoint.followingControlPointPosition - startPoint.position ) +
			   6f * oneMinusLocalT * localT * ( endPoint.precedingControlPointPosition - startPoint.followingControlPointPosition ) +
			   3f * localT * localT * ( endPoint.position - endPoint.precedingControlPointPosition );
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

	public Vector3 MoveAlongSpline( ref float normalizedT, float deltaMovement, 
									bool increasedAccuracy = false, int maximumNumberOfChecks = 20, float maximumError = 0.001f )
	{
		// Maybe that one is a better approach? https://www.geometrictools.com/Documentation/MovingAlongCurveSpecifiedSpeed.pdf

		if( Mathf.Approximately( deltaMovement, 0f ) )
			return GetPoint( normalizedT );

		if( maximumNumberOfChecks < 3 )
			maximumNumberOfChecks = 3;

		normalizedT = Mathf.Clamp01( normalizedT );

		Vector3 point = GetPoint( normalizedT );
		float deltaMovementSqr = deltaMovement * deltaMovement;
		bool isForwardDir = deltaMovement > 0;

		float bestNormalizedT;
		float maxNormalizedT, minNormalizedT;

		float error;
		Vector3 result;

		if( isForwardDir )
		{
			bestNormalizedT = ( 1f + normalizedT ) * 0.5f;

			maxNormalizedT = 1f;
			minNormalizedT = normalizedT;
		}
		else
		{
			bestNormalizedT = normalizedT * 0.5f;

			maxNormalizedT = normalizedT;
			minNormalizedT = 0f;
		}

		result = GetPoint( bestNormalizedT );

		if( !increasedAccuracy )
		{
			error = ( result - point ).sqrMagnitude - deltaMovementSqr;
		}
		else
		{
			float distance = GetLengthApproximately( normalizedT, bestNormalizedT, 10f );
			error = distance * distance - deltaMovementSqr;
		}

		if( !isForwardDir )
			error = -error;

		if( Mathf.Abs( error ) > maximumError )
		{
			for( int i = 0; i < maximumNumberOfChecks; i++ )
			{
				if( error > 0 )
				{
					maxNormalizedT = bestNormalizedT;
					bestNormalizedT = ( bestNormalizedT + minNormalizedT ) * 0.5f;
				}
				else
				{
					minNormalizedT = bestNormalizedT;
					bestNormalizedT = ( bestNormalizedT + maxNormalizedT ) * 0.5f;
				}

				result = GetPoint( bestNormalizedT );

				if( !increasedAccuracy )
				{
					error = ( result - point ).sqrMagnitude - deltaMovementSqr;
				}
				else
				{
					float distance = GetLengthApproximately( normalizedT, bestNormalizedT, 10f );
					error = distance * distance - deltaMovementSqr;
				}

				if( !isForwardDir )
					error = -error;

				if( Mathf.Abs( error ) <= maximumError )
				{
					break;
				}
			}
		}

		normalizedT = bestNormalizedT;
		return result;
	}

	public void AutoConstructSpline()
	{
		// Credit: http://www.codeproject.com/Articles/31859/Draw-a-Smooth-Curve-through-a-Set-of-2D-Points-wit

		int n = endPoints.Length - 1;

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
		{
			rhs[i] = 4 * endPoints[i].position + 2 * endPoints[i + 1].position;
		}

		rhs[0] = endPoints[0].position + 2 * endPoints[1].position;

		if( !loop )
			rhs[n - 1] = ( 8 * endPoints[n - 1].position + endPoints[n].position ) * 0.5f;
		else
		{
			rhs[n - 1] = 4 * endPoints[n - 1].position + 2 * endPoints[n].position;
			rhs[n] = ( 8 * endPoints[n].position + endPoints[0].position ) * 0.5f;
		}

		// Get first control points
		Vector3[] controlPoints = GetFirstControlPoints( rhs );

		for( int i = 0; i < n; i++ )
		{
			// First control point
			endPoints[i].followingControlPointPosition = controlPoints[i];

			if( loop )
			{
				endPoints[i + 1].precedingControlPointPosition = 2 * endPoints[i + 1].position - controlPoints[i + 1];
			}
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
			endPoints[0].precedingControlPointPosition = ( endPoints[0].position + controlPoints[n] ) * 0.5f;
			endPoints[0].followingControlPointLocalPosition = -endPoints[0].precedingControlPointLocalPosition;
		}
	}

	private static Vector3[] GetFirstControlPoints( Vector3[] rhs )
	{
		// Credit: http://www.codeproject.com/Articles/31859/Draw-a-Smooth-Curve-through-a-Set-of-2D-Points-wit

		int n = rhs.Length;
		Vector3[] x = new Vector3[n]; // Solution vector.
		float[] tmp = new float[n]; // Temp workspace.

		float b = 2f;
		x[0] = rhs[0] / b;
		for( int i = 1; i < n; i++ ) // Decomposition and forward substitution.
		{
			float val = 1f / b;
			tmp[i] = val;
			b = ( i < n - 1 ? 4f : 3.5f ) - val;
			x[i] = ( rhs[i] - x[i - 1] ) / b;
		}

		for( int i = 1; i < n; i++ )
		{
			x[n - i - 1] -= tmp[n - i] * x[n - i]; // Backsubstitution.
		}

		return x;
	}

	public void AutoConstructSpline2()
	{
		// Credit: http://stackoverflow.com/questions/3526940/how-to-create-a-cubic-bezier-curve-when-given-n-points-in-3d

		for( int i = 0; i < endPoints.Length; i++ )
		{
			Vector3 pMinus1, p1, p2;

			if( i == 0 )
			{
				if( loop )
					pMinus1 = endPoints[endPoints.Length - 1].position;
				else
					pMinus1 = endPoints[0].position;
			}
			else
			{
				pMinus1 = endPoints[i - 1].position;
			}

			if( loop )
			{
				p1 = endPoints[( i + 1 ) % endPoints.Length].position;
				p2 = endPoints[( i + 2 ) % endPoints.Length].position;
			}
			else
			{
				if( i < endPoints.Length - 2 )
				{
					p1 = endPoints[i + 1].position;
					p2 = endPoints[i + 2].position;
				}
				else if( i == endPoints.Length - 2 )
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

			if( i < endPoints.Length - 1 )
				endPoints[i + 1].precedingControlPointPosition = p1 - ( p2 - endPoints[i].position ) / 6f;
			else if( loop )
				endPoints[0].precedingControlPointPosition = p1 - ( p2 - endPoints[i].position ) / 6f;
		}
	}

	/*public void AutoConstructSpline3()
	{
		// Todo? http://www.math.ucla.edu/~baker/149.1.02w/handouts/dd_splines.pdf
	}*/

	private float AccuracyToStepSize( float accuracy )
	{
		if( accuracy <= 0f )
			return 0.2f;
		
		return Mathf.Clamp( 1f / accuracy, 0.001f, 0.2f );
	}

	public void SwapPointsAt( int index1, int index2 )
	{
		if( index1 == index2 )
		{
			Debug.LogError( "Indices can't be equal to each other" );
			return;
		}

		if( index1 < 0 || index1 >= endPoints.Length || index2 < 0 || index2 >= endPoints.Length )
		{
			Debug.LogError( "Indices must be in range [0," + ( endPoints.Length - 1 ) + "]" );
			return;
		}

		BezierPoint point1 = endPoints[index1];
		endPoints[index1] = endPoints[index2];
		endPoints[index2] = point1;
	}

	public void Revalidate()
	{
		m_localToWorldMatrix = cachedTransform.localToWorldMatrix;
		m_worldToLocalMatrix = cachedTransform.worldToLocalMatrix;

		for( int i = 0; i < endPoints.Length; i++ )
		{
			endPoints[i].Revalidate();
		}
	}

	void Reset()
	{
		cachedTransform = transform;

		BezierPoint point0 = new BezierPoint( this, Vector3.back );
		BezierPoint point1 = new BezierPoint( this, Vector3.forward );

		endPoints = new BezierPoint[] { point0, point1 };
	}
}