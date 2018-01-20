using UnityEngine;
using UnityEditor;

namespace BezierSolution
{
	[CustomEditor( typeof( BezierSpline ) )]
	public class BezierSplineEditor : Editor
	{
		private BezierSpline spline;

		void OnEnable()
		{
			spline = target as BezierSpline;
			spline.Refresh();

			Undo.undoRedoPerformed -= OnUndoRedo;
			Undo.undoRedoPerformed += OnUndoRedo;
		}

		void OnDisable()
		{
			Undo.undoRedoPerformed -= OnUndoRedo;
		}

		void OnSceneGUI()
		{
			BezierUtils.DrawSplineDetailed( spline );

			for( int i = 0; i < spline.Count; i++ )
				BezierUtils.DrawBezierPoint( spline[i], i + 1, false );
		}

		public override void OnInspectorGUI()
		{
			BezierUtils.DrawSplineInspectorGUI( spline );
		}

		private void OnUndoRedo()
		{
			if( spline != null && !spline.Equals( null ) )
				spline.Refresh();

			Repaint();
		}

		private bool HasFrameBounds()
		{
			return true;
		}

		private Bounds OnGetFrameBounds()
		{
			return new Bounds( spline.transform.position, Vector3.one );
		}
	}
}