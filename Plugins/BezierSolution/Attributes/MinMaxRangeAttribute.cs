using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BezierSolution
{
	public class MinMaxRangeAttribute : PropertyAttribute
	{
		public float min;
		public float max;

		public MinMaxRangeAttribute( float min, float max )
		{
			this.min = min;
			this.max = max;
		}
	}
}

#if UNITY_EDITOR
namespace BezierSolution.Extras
{
	[CustomPropertyDrawer( typeof( MinMaxRangeAttribute ) )]
	public class MixMaxRangeAttributeDrawer : PropertyDrawer
	{
		private const float MIN_MAX_SLIDER_TEXT_FIELD_WIDTH = 45f;

		// Min-max slider credit: https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Editor/Mono/Inspector/LightEditor.cs#L328-L363
		public override void OnGUI( Rect position, SerializedProperty property, GUIContent label )
		{
			MinMaxRangeAttribute minMaxRange = attribute as MinMaxRangeAttribute;

			SerializedProperty minProp = property.FindPropertyRelative( "x" );
			SerializedProperty maxProp = property.FindPropertyRelative( "y" );

			position = EditorGUI.PrefixLabel( position, label );
			EditorGUI.BeginProperty( position, GUIContent.none, property );

			Rect minRect = new Rect( position ) { width = MIN_MAX_SLIDER_TEXT_FIELD_WIDTH };
			Rect maxRect = new Rect( position ) { xMin = position.xMax - MIN_MAX_SLIDER_TEXT_FIELD_WIDTH };
			Rect sliderRect = new Rect( position ) { xMin = minRect.xMax + 5f, xMax = maxRect.xMin - 5f };

			EditorGUI.BeginChangeCheck();

			EditorGUI.PropertyField( minRect, minProp, GUIContent.none );

			Vector2 value = property.vector2Value;
			EditorGUI.BeginChangeCheck();
			EditorGUI.MinMaxSlider( sliderRect, ref value.x, ref value.y, minMaxRange.min, minMaxRange.max );
			if( EditorGUI.EndChangeCheck() )
				property.vector2Value = value;

			EditorGUI.PropertyField( maxRect, maxProp, GUIContent.none );

			if( EditorGUI.EndChangeCheck() )
			{
				float x = minProp.floatValue;
				float y = maxProp.floatValue;

				if( x < minMaxRange.min || x > minMaxRange.max )
					minProp.floatValue = Mathf.Clamp( x, minMaxRange.min, minMaxRange.max );
				if( y < minMaxRange.min || y > minMaxRange.max )
					maxProp.floatValue = Mathf.Clamp( y, minMaxRange.min, minMaxRange.max );
			}

			EditorGUI.EndProperty();
		}
	}
}
#endif