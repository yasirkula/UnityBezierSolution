using System.IO;
using UnityEditor;
using UnityEngine;

namespace BezierSolution
{
	public static class BezierSplineExport
	{
		[System.Serializable]
		private class SavedSpline
		{
			[System.Serializable]
			public class SavedPoint
			{
				public Vector3 position;
				public Quaternion rotation;
				public Vector3 scale;

				public BezierPoint.HandleMode handleMode;

				public Vector3 precedingPosition;
				public Vector3 followingPosition;
			}

			public bool loop;
			public SavedPoint[] points;
		}

		[MenuItem( "CONTEXT/BezierSpline/Export" )]
		static void ExportBezierSpline( MenuCommand command )
		{
			string savePath = EditorUtility.SaveFilePanel( "Export to", null, "spline", "json" );
			if( string.IsNullOrEmpty( savePath ) )
				return;

			BezierSpline spline = (BezierSpline) command.context;

			SavedSpline savedSpline = new SavedSpline()
			{
				loop = spline.loop,
				points = new SavedSpline.SavedPoint[spline.Count]
			};

			for( int i = 0; i < spline.Count; i++ )
			{
				BezierPoint point = spline[i];

				savedSpline.points[i] = new SavedSpline.SavedPoint()
				{
					position = point.localPosition,
					rotation = point.localRotation,
					scale = point.localScale,
					handleMode = point.handleMode,
					precedingPosition = point.precedingControlPointLocalPosition,
					followingPosition = point.followingControlPointLocalPosition
				};
			}

			File.WriteAllText( savePath, JsonUtility.ToJson( savedSpline, false ) );
			Debug.Log( "Exported to: " + savePath );
		}

		[MenuItem( "CONTEXT/BezierSpline/Import" )]
		static void ImportBezierSpline( MenuCommand command )
		{
			string loadPath = EditorUtility.OpenFilePanel( "Import spline", null, "json" );
			if( string.IsNullOrEmpty( loadPath ) )
				return;

			SavedSpline savedSpline = JsonUtility.FromJson<SavedSpline>( File.ReadAllText( loadPath ) );
			if( savedSpline == null || savedSpline.points == null || savedSpline.points.Length < 2 )
			{
				Debug.LogError( "Invalid saved data!" );
				return;
			}

			BezierSpline spline = (BezierSpline) command.context;
			spline.loop = savedSpline.loop;

			if( spline.Count > savedSpline.points.Length )
			{
				for( int i = spline.Count - 1; i >= savedSpline.points.Length; i-- )
					spline.RemovePointAt( i );
			}
			else
			{
				for( int i = spline.Count; i < savedSpline.points.Length; i++ )
					spline.InsertNewPointAt( i );
			}
			
			for( int i = 0; i < savedSpline.points.Length; i++ )
			{
				BezierPoint point = spline[i];
				SavedSpline.SavedPoint savedPoint = savedSpline.points[i];

				point.localPosition = savedPoint.position;
				point.localRotation = savedPoint.rotation;
				point.localScale = savedPoint.scale;

				point.handleMode = savedPoint.handleMode;

				point.precedingControlPointLocalPosition = savedPoint.precedingPosition;
				point.followingControlPointLocalPosition = savedPoint.followingPosition;
			}
		}
	}
}