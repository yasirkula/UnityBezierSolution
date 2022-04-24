using UnityEngine;

namespace BezierSolution
{
	public enum SplineAutoConstructMode { None = 0, Linear = 1, Smooth1 = 2, Smooth2 = 3 };

	[System.Flags]
	internal enum InternalDirtyFlags
	{
		None = 0,
		EndPointTransformChange = 1 << 1,
		ControlPointPositionChange = 1 << 2,
		NormalChange = 1 << 3,
		NormalOffsetChange = 1 << 4,
		ExtraDataChange = 1 << 5,
		All = EndPointTransformChange | ControlPointPositionChange | NormalChange | NormalOffsetChange | ExtraDataChange
	};

	[System.Flags]
	public enum DirtyFlags
	{
		None = 0,
		SplineShapeChanged = 1 << 1,
		NormalsChanged = 1 << 2,
		ExtraDataChanged = 1 << 3,
		All = SplineShapeChanged | NormalsChanged | ExtraDataChanged
	};

	[System.Flags]
	public enum PointCacheFlags
	{
		None = 0,
		Positions = 1 << 1,
		Normals = 1 << 2,
		Tangents = 1 << 3,
		Bitangents = 1 << 4,
		ExtraDatas = 1 << 5,
		All = Positions | Normals | Tangents | Bitangents | ExtraDatas
	};

	public delegate void SplineChangeDelegate( BezierSpline spline, DirtyFlags dirtyFlags );
	public delegate BezierPoint.ExtraData ExtraDataLerpFunction( BezierPoint.ExtraData data1, BezierPoint.ExtraData data2, float normalizedT );

	public partial class BezierSpline
	{
		private static readonly ExtraDataLerpFunction defaultExtraDataLerpFunction = BezierPoint.ExtraData.LerpUnclamped;

		public struct Segment
		{
			public readonly BezierPoint point1, point2;
			public readonly float localT;

			public Segment( BezierPoint point1, BezierPoint point2, float localT )
			{
				this.point1 = point1;
				this.point2 = point2;
				this.localT = localT;
			}

			public float GetNormalizedT() { return GetNormalizedT( localT ); }
			public float GetNormalizedT( float localT )
			{
				BezierSpline spline = point1.spline;
				return ( point1.index + localT ) / ( spline.m_loop ? spline.Count : ( spline.Count - 1 ) );
			}

			public Vector3 GetPoint() { return GetPoint( localT ); }
			public Vector3 GetPoint( float localT )
			{
				float oneMinusLocalT = 1f - localT;

				return oneMinusLocalT * oneMinusLocalT * oneMinusLocalT * point1.position +
					   3f * oneMinusLocalT * oneMinusLocalT * localT * point1.followingControlPointPosition +
					   3f * oneMinusLocalT * localT * localT * point2.precedingControlPointPosition +
					   localT * localT * localT * point2.position;
			}

			public Vector3 GetTangent() { return GetTangent( localT ); }
			public Vector3 GetTangent( float localT )
			{
				float oneMinusLocalT = 1f - localT;

				return 3f * oneMinusLocalT * oneMinusLocalT * ( point1.followingControlPointPosition - point1.position ) +
					   6f * oneMinusLocalT * localT * ( point2.precedingControlPointPosition - point1.followingControlPointPosition ) +
					   3f * localT * localT * ( point2.position - point2.precedingControlPointPosition );
			}

			public Vector3 GetNormal() { return GetNormal( localT ); }
			public Vector3 GetNormal( float localT )
			{
				Vector3[] intermediateNormals = point1.intermediateNormals;
				if( intermediateNormals != null && intermediateNormals.Length > 0 )
				{
					localT = Mathf.Clamp01( localT ) * ( intermediateNormals.Length - 1 );
					int localStartIndex = (int) localT;

					return ( localStartIndex < intermediateNormals.Length - 1 ) ? Vector3.LerpUnclamped( intermediateNormals[localStartIndex], intermediateNormals[localStartIndex + 1], localT - localStartIndex ) : intermediateNormals[localStartIndex];
				}

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

			public BezierPoint.ExtraData GetExtraData() { return defaultExtraDataLerpFunction( point1.extraData, point2.extraData, localT ); }
			public BezierPoint.ExtraData GetExtraData( float localT ) { return defaultExtraDataLerpFunction( point1.extraData, point2.extraData, localT ); }
			public BezierPoint.ExtraData GetExtraData( ExtraDataLerpFunction lerpFunction ) { return lerpFunction( point1.extraData, point2.extraData, localT ); }
			public BezierPoint.ExtraData GetExtraData( float localT, ExtraDataLerpFunction lerpFunction ) { return lerpFunction( point1.extraData, point2.extraData, localT ); }
		}

		public class EvenlySpacedPointsHolder
		{
			public readonly BezierSpline spline;
			public readonly float splineLength;
			public readonly float[] uniformNormalizedTs;

			public EvenlySpacedPointsHolder( BezierSpline spline, float splineLength, float[] uniformNormalizedTs )
			{
				this.spline = spline;
				this.splineLength = splineLength;
				this.uniformNormalizedTs = uniformNormalizedTs;
			}

			public float GetNormalizedTAtDistance( float distance )
			{
				return GetNormalizedTAtPercentage( distance / splineLength );
			}

			public float GetNormalizedTAtPercentage( float percentage )
			{
				if( !spline.loop )
				{
					if( percentage <= 0f )
						return 0f;
					else if( percentage >= 1f )
						return 1f;
				}
				else
				{
					while( percentage < 0f )
						percentage += 1f;
					while( percentage >= 1f )
						percentage -= 1f;
				}

				float indexRaw = ( uniformNormalizedTs.Length - 1 ) * percentage;
				int index = (int) indexRaw;
				return Mathf.LerpUnclamped( uniformNormalizedTs[index], uniformNormalizedTs[index + 1], indexRaw - index );
			}

			public float GetPercentageAtNormalizedT( float normalizedT )
			{
				if( !spline.loop )
				{
					if( normalizedT <= 0f )
						return 0f;
					else if( normalizedT >= 1f )
						return 1f;
				}
				else
				{
					while( normalizedT < 0f )
						normalizedT += 1f;
					while( normalizedT >= 1f )
						normalizedT -= 1f;
				}

				// Perform binary search
				int lowerBound = 0;
				int upperBound = uniformNormalizedTs.Length - 1;
				while( lowerBound <= upperBound )
				{
					int index = lowerBound + ( ( upperBound - lowerBound ) >> 1 );
					float arrElement = uniformNormalizedTs[index];
					if( arrElement < normalizedT )
						lowerBound = index + 1;
					else if( arrElement > normalizedT )
						upperBound = index - 1;
					else
						return index / (float) ( uniformNormalizedTs.Length - 1 );
				}

				float inverseLerp = ( normalizedT - uniformNormalizedTs[lowerBound] ) / ( uniformNormalizedTs[lowerBound - 1] - uniformNormalizedTs[lowerBound] );
				return ( lowerBound - inverseLerp ) / ( uniformNormalizedTs.Length - 1 );
			}
		}

		public class PointCache
		{
			public readonly Vector3[] positions, normals, tangents, bitangents;
			public readonly BezierPoint.ExtraData[] extraDatas;
			public readonly bool loop;

			public PointCache( Vector3[] positions, Vector3[] normals, Vector3[] tangents, Vector3[] bitangents, BezierPoint.ExtraData[] extraDatas, bool loop )
			{
				this.positions = positions;
				this.normals = normals;
				this.tangents = tangents;
				this.bitangents = bitangents;
				this.extraDatas = extraDatas;
				this.loop = loop;
			}

			public Vector3 GetPoint( float percentage ) { return LerpArray( positions, percentage ); }
			public Vector3 GetNormal( float percentage ) { return LerpArray( normals, percentage ); }
			public Vector3 GetTangent( float percentage ) { return LerpArray( tangents, percentage ); }
			public Vector3 GetBitangent( float percentage ) { return LerpArray( bitangents, percentage ); }

			public BezierPoint.ExtraData GetExtraData( float percentage ) { return GetExtraData( percentage, defaultExtraDataLerpFunction ); }
			public BezierPoint.ExtraData GetExtraData( float percentage, ExtraDataLerpFunction lerpFunction )
			{
				if( !loop )
				{
					if( percentage <= 0f )
						return extraDatas[0];
					else if( percentage >= 1f )
						return extraDatas[extraDatas.Length - 1];
				}
				else
				{
					while( percentage < 0f )
						percentage += 1f;
					while( percentage >= 1f )
						percentage -= 1f;
				}

				float t = percentage * ( loop ? extraDatas.Length : ( extraDatas.Length - 1 ) );

				int startIndex = (int) t;
				int endIndex = startIndex + 1;

				if( endIndex == extraDatas.Length )
					endIndex = 0;

				return lerpFunction( extraDatas[startIndex], extraDatas[endIndex], t - startIndex );
			}

			private Vector3 LerpArray( Vector3[] array, float percentage )
			{
				if( !loop )
				{
					if( percentage <= 0f )
						return array[0];
					else if( percentage >= 1f )
						return array[array.Length - 1];
				}
				else
				{
					while( percentage < 0f )
						percentage += 1f;
					while( percentage >= 1f )
						percentage -= 1f;
				}

				float t = percentage * ( loop ? array.Length : ( array.Length - 1 ) );

				int startIndex = (int) t;
				int endIndex = startIndex + 1;

				if( endIndex == array.Length )
					endIndex = 0;

				return Vector3.LerpUnclamped( array[startIndex], array[endIndex], t - startIndex );
			}
		}
	}

	public partial class BezierPoint
	{
		public enum HandleMode { Free, Aligned, Mirrored };

		[System.Serializable]
		public struct ExtraData
		{
			public float c1, c2, c3, c4;

			public ExtraData( float c1 = 0f, float c2 = 0f, float c3 = 0f, float c4 = 0f )
			{
				this.c1 = c1;
				this.c2 = c2;
				this.c3 = c3;
				this.c4 = c4;
			}

			public static ExtraData Lerp( ExtraData a, ExtraData b, float t )
			{
				t = Mathf.Clamp01( t );
				return new ExtraData(
					a.c1 + ( b.c1 - a.c1 ) * t,
					a.c2 + ( b.c2 - a.c2 ) * t,
					a.c3 + ( b.c3 - a.c3 ) * t,
					a.c4 + ( b.c4 - a.c4 ) * t );
			}

			public static ExtraData LerpUnclamped( ExtraData a, ExtraData b, float t )
			{
				return new ExtraData(
					a.c1 + ( b.c1 - a.c1 ) * t,
					a.c2 + ( b.c2 - a.c2 ) * t,
					a.c3 + ( b.c3 - a.c3 ) * t,
					a.c4 + ( b.c4 - a.c4 ) * t );
			}

			public static implicit operator ExtraData( Vector2 v ) { return new ExtraData( v.x, v.y ); }
			public static implicit operator ExtraData( Vector3 v ) { return new ExtraData( v.x, v.y, v.z ); }
			public static implicit operator ExtraData( Vector4 v ) { return new ExtraData( v.x, v.y, v.z, v.w ); }
			public static implicit operator ExtraData( Quaternion q ) { return new ExtraData( q.x, q.y, q.z, q.w ); }
			public static implicit operator ExtraData( Rect r ) { return new ExtraData( r.xMin, r.yMin, r.width, r.height ); }
#if UNITY_2017_2_OR_NEWER
			public static implicit operator ExtraData( Vector2Int v ) { return new ExtraData( v.x, v.y ); }
			public static implicit operator ExtraData( Vector3Int v ) { return new ExtraData( v.x, v.y, v.z ); }
			public static implicit operator ExtraData( RectInt r ) { return new ExtraData( r.xMin, r.yMin, r.width, r.height ); }
#endif

			public static implicit operator Vector2( ExtraData v ) { return new Vector2( v.c1, v.c2 ); }
			public static implicit operator Vector3( ExtraData v ) { return new Vector3( v.c1, v.c2, v.c3 ); }
			public static implicit operator Vector4( ExtraData v ) { return new Vector4( v.c1, v.c2, v.c3, v.c4 ); }
			public static implicit operator Quaternion( ExtraData v ) { return new Quaternion( v.c1, v.c2, v.c3, v.c4 ); }
			public static implicit operator Rect( ExtraData v ) { return new Rect( v.c1, v.c2, v.c3, v.c4 ); }
#if UNITY_2017_2_OR_NEWER
			public static implicit operator Vector2Int( ExtraData v ) { return new Vector2Int( Mathf.RoundToInt( v.c1 ), Mathf.RoundToInt( v.c2 ) ); }
			public static implicit operator Vector3Int( ExtraData v ) { return new Vector3Int( Mathf.RoundToInt( v.c1 ), Mathf.RoundToInt( v.c2 ), Mathf.RoundToInt( v.c3 ) ); }
			public static implicit operator RectInt( ExtraData v ) { return new RectInt( Mathf.RoundToInt( v.c1 ), Mathf.RoundToInt( v.c2 ), Mathf.RoundToInt( v.c3 ), Mathf.RoundToInt( v.c4 ) ); }
#endif

			public static bool operator ==( ExtraData d1, ExtraData d2 ) { return d1.c1 == d2.c1 && d1.c2 == d2.c2 && d1.c3 == d2.c3 && d1.c4 == d2.c4; }
			public static bool operator !=( ExtraData d1, ExtraData d2 ) { return d1.c1 != d2.c1 || d1.c2 != d2.c2 || d1.c3 != d2.c3 || d1.c4 != d2.c4; }

			public override bool Equals( object obj ) { return obj is ExtraData && this == (ExtraData) obj; }
			public override int GetHashCode() { return unchecked((int) ( ( ( ( 17 * 23 + c1 ) * 23 + c2 ) * 23 + c3 ) * 23 + c4 )); }
			public override string ToString() { return ( (Vector4) this ).ToString(); }
		}
	}
}