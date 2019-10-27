using UnityEditor;
using UnityEngine;

namespace BezierSolution.Extras
{
	[CustomPropertyDrawer( typeof( BezierPoint.ExtraData ) )]
	public class BezierPointExtraDataDrawer : PropertyDrawer
	{
		public override void OnGUI( Rect position, SerializedProperty property, GUIContent label )
		{
			EditorGUI.BeginProperty( position, label, property );
			position = EditorGUI.PrefixLabel( position, GUIUtility.GetControlID( FocusType.Passive ), label );

			float quarterWidth = position.width * 0.25f;
			Rect c1Rect = new Rect( position.x, position.y, quarterWidth, position.height );
			Rect c2Rect = new Rect( position.x + quarterWidth, position.y, quarterWidth, position.height );
			Rect c3Rect = new Rect( position.x + 2f * quarterWidth, position.y, quarterWidth, position.height );
			Rect c4Rect = new Rect( position.x + 3f * quarterWidth, position.y, quarterWidth, position.height );

			EditorGUI.PropertyField( c1Rect, property.FindPropertyRelative( "c1" ), GUIContent.none );
			EditorGUI.PropertyField( c2Rect, property.FindPropertyRelative( "c2" ), GUIContent.none );
			EditorGUI.PropertyField( c3Rect, property.FindPropertyRelative( "c3" ), GUIContent.none );
			EditorGUI.PropertyField( c4Rect, property.FindPropertyRelative( "c4" ), GUIContent.none );

			EditorGUI.EndProperty();
		}
	}
}