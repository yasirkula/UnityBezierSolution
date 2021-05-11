using UnityEngine;
using UnityEditor;

namespace BezierSolution.Extras
{
	[CustomEditor( typeof( BezierSpline ) )]
	[CanEditMultipleObjects]
	public class BezierSplineEditor : Editor
	{
		internal BezierSpline[] allSplines;

		public static BezierSplineEditor ActiveEditor { get; private set; }

		private void OnEnable()
		{
			Object[] splines = targets;
			allSplines = new BezierSpline[splines.Length];
			for( int i = 0; i < splines.Length; i++ )
			{
				BezierSpline spline = (BezierSpline) splines[i];
				if( spline )
					spline.Refresh();

				allSplines[i] = spline;
			}

			ActiveEditor = this;

			Undo.undoRedoPerformed -= OnUndoRedo;
			Undo.undoRedoPerformed += OnUndoRedo;
		}

		private void OnDisable()
		{
			ActiveEditor = null;

			Undo.undoRedoPerformed -= OnUndoRedo;
		}

		private void OnSceneGUI()
		{
			BezierSpline spline = (BezierSpline) target;
			BezierUtils.DrawSplineDetailed( spline );

			for( int i = 0; i < spline.Count; i++ )
				BezierUtils.DrawBezierPoint( spline[i], i + 1, false );
		}

		public override void OnInspectorGUI()
		{
			BezierUtils.DrawSplineInspectorGUI( allSplines );
		}

		private void OnUndoRedo()
		{
			for( int i = 0; i < allSplines.Length; i++ )
			{
				if( allSplines[i] )
				{
					allSplines[i].dirtyFlags |= InternalDirtyFlags.All;
					allSplines[i].Refresh();
				}
			}

			Repaint();
		}

		private bool HasFrameBounds()
		{
			return !serializedObject.isEditingMultipleObjects;
		}

		private Bounds OnGetFrameBounds()
		{
			return new Bounds( ( (BezierSpline) target ).transform.position, new Vector3( 1f, 1f, 1f ) );
		}
	}
}