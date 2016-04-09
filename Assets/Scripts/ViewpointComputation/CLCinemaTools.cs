/*
-----------------------------------------------------------------------------
This source file is part of ViewpointComputationLib (a viewpoint computation library)
For more info on the project, contact Roberto Ranon at roberto.ranon@uniud.it.

Copyright (c) 2013- University of Udine, Italy - http://hcilab.uniud.it
Also see acknowledgements in readme.txt
-----------------------------------------------------------------------------

 CLCinemaTools.cs: file defining classes for cinematographic concepts 

-----------------------------------------------------------------------------
*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// Shot lenght type. From http://www.mediacollege.com/video/shots/
/// </summary>
public enum ShotLenghtType {
    
    MediumShot, // head and torso occupy 0.9 of screen height
    MediumCloseUp, // head and shoulders occupy 0.7-0.8 of screen height
	CloseUp // head occupy 0.9 of screen height

}

/// <summary>
/// Vertical View Angle type.
/// </summary>
public enum VerticalViewAngleType {

	TopView, 
	HighAngle, 
	MediumAngle,
	LowAngle,
	BottomView
}

/// <summary>
/// Horizontal View Angle type.
/// </summary>
public enum HorizontalViewAngleType {

	Frontal, 
	Back, 
	Lateral,
	FrontalThreeQuarter,
	BackThreeQuarter
}


/// <summary>
/// Defines a character target of our viewpoint computation problem. A character target has a pre-defined structure:
/// head - shoulders - torso - right tight - left tight, which are colliders 
/// </summary>
public class CLCharacterTarget : CLTarget {


	List<GameObject> allBodyColliders;

	/// <summary>
	/// Initializes a new instance of the <see cref="CLCharacterTarget"/> class.
	/// </summary>
	/// <param name="layersToExclude">Layers to exclude for ray casting</param>
	/// <param name="sceneObj"> Corresponding scene object.</param>
	/// <param name="_colliders">Colliders (list of objects to be used for ray casting)</param>
	/// <param name="_nRays">number of rays to use for checking visibility</param>
	public CLCharacterTarget (int layersToExclude, GameObject sceneObj, List<GameObject> _colliders, int _nRays = 8) :
	base (layersToExclude, sceneObj, new List<GameObject>(), _colliders, false, _nRays) {

		if (_colliders.Count == 0) {
			string characterName = sceneObj.name;
			_colliders.Add( GameObject.Find(characterName + "_HeadCollision"));
			_colliders.Add( GameObject.Find(characterName + "_ShouldersCollision"));
			_colliders.Add( GameObject.Find(characterName + "_TorsoCollision"));
			_colliders.Add( GameObject.Find(characterName + "_LeftThighCollision"));
			_colliders.Add( GameObject.Find(characterName + "_RightThighCollision"));
			colliders = new List<GameObject>( _colliders );
		} 

		allBodyColliders = new List<GameObject> (_colliders);
	}





	/// <summary>
	/// Sets the shot lenght.
	/// </summary>
	/// <param name="sl">Sl.</param>
	public void SetShotLenght ( ShotLenghtType sl ) {

		switch (sl) {
            case ShotLenghtType.CloseUp: // only head
                
                colliders = new List<GameObject> () {allBodyColliders [0]};
                break;
     
            case ShotLenghtType.MediumCloseUp: // head + shoulders
            
                colliders = new List<GameObject>() { allBodyColliders[0], allBodyColliders[1] };
                break;

            case ShotLenghtType.MediumShot: //head and torso
                
                colliders = new List<GameObject>() { allBodyColliders[0], allBodyColliders[2] };
                break;

		    default:
			
                break;


		}

	}

	/// <summary>
	/// Computes the min angle in radians between the provided v vector and one of the local axes of the target 
	/// </summary>
	public override float ComputeAngleWith (Vector3 v, TargetUtils.Axis axis)
	{
		Vector3 v2;
		switch (axis) {
		case TargetUtils.Axis.RIGHT:
			// we use the local coordinate system of the gameobject in gameObjectRef
			v2 = allBodyColliders[2].transform.right;
			break;
		case TargetUtils.Axis.UP:
			v2 = allBodyColliders[2].transform.up;
			break;
		case TargetUtils.Axis.WORLD_UP:
			v2 = Vector3.up;
			break;
		default: // forward
			v2 = allBodyColliders[2].transform.forward;
			break;
		}

		return Vector3.Angle (v, v2);
	}


}


/// <summary>
/// Defines a screen size property using traditional shot lenght sizes.
/// </summary>
public	class CLShotLenghtProperty : CLSizeProperty
{

	/// <summary>
	/// Initializes a new instance of the <see cref="CLShotLenghtProperty"/> class. This is a variant of the 
	/// CLSizeProperty class which is specific to characters and typical shot lenghts used in cinema.
	/// </summary>
	/// <param name="shotLenght">size mode.</param>
	/// <param name="_name">property name</param>
	/// <param name="_targets">property targets</param>
	/// <param name="_shotLenght">shot lenght</param>
	public CLShotLenghtProperty (string _name, List<CLTarget> _targets, string _shotLenght) :
	this ( _name, _targets, ShotLenghtFromString( _shotLenght )) {}	

	/// <summary>
	/// Initializes a new instance of the <see cref="CLShotLenghtProperty"/> class. This is a variant of the 
	/// CLSizeProperty class which is specific to characters and typical shot lenghts used in cinema.
	/// </summary>
	/// <param name="shotLenght">size mode.</param>
	/// <param name="_name">property name</param>
	/// <param name="_targets">property targets</param>
	/// <param name="_shotLenght">shot lenght</param>
	public CLShotLenghtProperty (string _name, List<CLTarget> _targets, ShotLenghtType _shotLenght) :
	base (CLSizeProperty.SizeMode.HEIGHT, _name, _targets, null )
	{
		
		if (_targets [0] is CLCharacterTarget) {
			CLCharacterTarget ct = (CLCharacterTarget)_targets [0];
			ct.SetShotLenght (_shotLenght);
		}
		cost = 1.0f;
		List<float> sizeSatFuncCtrlX, sizeSatFuncCtrlY;

		switch (_shotLenght) {

		case ShotLenghtType.MediumCloseUp: // head and shoulders 

			sizeSatFuncCtrlX = new List<float> { 0.0f, 0.49f, 0.5f, 0.8f, 1.0f, 1.1f, 1.4f };
			sizeSatFuncCtrlY = new List<float> { 0.0f, 0.1f , 0.8f, 1.0f, 0.8f, 0.1f, 0.0f };
			this.satFunction = new CLLinearSplineSatFunction (sizeSatFuncCtrlX, sizeSatFuncCtrlY);
			break;

		case ShotLenghtType.CloseUp: // head

			sizeSatFuncCtrlX = new List<float> { 0.0f, 0.5f, 0.7f, 0.8f, 0.9f, 1.1f, 2.0f };
			sizeSatFuncCtrlY = new List<float> { 0.0f, 0.1f, 0.8f, 1.0f, 0.8f, 0.1f, 0.0f };
			this.satFunction = new CLLinearSplineSatFunction (sizeSatFuncCtrlX, sizeSatFuncCtrlY);
			break;

		case ShotLenghtType.MediumShot: // head, and torso

			sizeSatFuncCtrlX = new List<float> { 0.0f, 0.5f, 0.7f, 0.8f, 0.9f, 1.1f, 2.0f };
			sizeSatFuncCtrlY = new List<float> { 0.0f, 0.1f, 0.8f, 1.0f, 0.8f, 0.1f, 0.0f };
			this.satFunction = new CLLinearSplineSatFunction (sizeSatFuncCtrlX, sizeSatFuncCtrlY);
			break;

		default:
			break;


		}
	}

	static ShotLenghtType ShotLenghtFromString( string _shotLenght ) {
		switch (_shotLenght) {

		case "MCU":
			return ShotLenghtType.MediumCloseUp;
			break;
		case "CU":
			return ShotLenghtType.CloseUp;
			break;
		case "MS":
			return ShotLenghtType.MediumShot;
			break;
		default:
			return ShotLenghtType.MediumCloseUp; 	

		}
	}
}

/// <summary>
/// Defines visibility property using a minimum desired visibility.
/// </summary>
public class CLVisibilityProperty : CLOcclusionProperty {


	/// <summary>
	/// Initializes a new instance of the <see cref="CLVisibilityProperty"/> class.
	/// </summary>
	/// <param name="_name">property name.</param>
	/// <param name="_targets">property targets.</param>
	/// <param name="minimumDesiredVisibility">Minimum desired visibility.</param>
	public CLVisibilityProperty( string _name, List<CLTarget> _targets, float minimumDesiredVisibility ) :
	base ( _name, _targets, null, true, true)
	{

		List<float> satFuncCtrlX, satFuncCtrlY;
		// two cases: minimumDesiredVisibility < 0.95f, minimumDesiredVisibility = 1.0f
		if (minimumDesiredVisibility <= 0.95f) {
			satFuncCtrlX = new List<float> {
				0.0f,
				minimumDesiredVisibility,
				minimumDesiredVisibility + 0.04f,
				1.0f
			}; 
			satFuncCtrlY = new List<float> { 1.0f, 0.9f, 0.1f, 0.0f };

		} else {

			satFuncCtrlX = new List<float> { 0.0f, 0.1f, 1.0f };
			satFuncCtrlY = new List<float> { 1.0f, 0.1f, 0.0f };
		}
		this.satFunction = new CLLinearSplineSatFunction (satFuncCtrlX, satFuncCtrlY);


	}


}


/// <summary>
/// Defines a horizontal view angle property using traditional camera angles
/// </summary>
public	class CLHorizontalViewAngle : CLOrientationProperty {


	public CLHorizontalViewAngle( string name, List<CLTarget> _targets, HorizontalViewAngleType viewAngle ) : 
	base (CLOrientationProperty.OrientationMode.HORIZONTAL, name, _targets, null ) {

		cost = 1.0f;
		List<float> satFuncCtrlX, satFuncCtrlY;
		
		switch (viewAngle) {
			
		case HorizontalViewAngleType.Frontal:
			
			satFuncCtrlX = new List<float> { 0.0f, 90.0f, 180.0f };
			satFuncCtrlY = new List<float> { 0.0f, 1.0f, 0.0f };
			this.satFunction = new CLLinearSplineSatFunction (satFuncCtrlX, satFuncCtrlY);
			break;
			
		default:
			break;
			
			
		}



	}



}


/// <summary>
/// Defines a vertical view angle property using traditional camera angles
/// </summary>
public	class CLVerticalViewAngle : CLOrientationProperty {


	public CLVerticalViewAngle( string _name, List<CLTarget> _targets, string _viewAngle ) :
	this( _name, _targets, AngleFromString( _viewAngle )){}

	/// <summary>
	/// Initializes a new instance of the <see cref="CLVerticalViewAngle"/> class.
	/// </summary>
	/// <param name="_name">property name.</param>
	/// <param name="_targets">Targets.</param>
	/// <param name="_viewAngle">View angle.</param>
	public CLVerticalViewAngle( string _name, List<CLTarget> _targets, VerticalViewAngleType _viewAngle ) : 
	base (CLOrientationProperty.OrientationMode.VERTICAL, _name, _targets, null ) {

		cost = 1.0f;
		List<float> satFuncCtrlX, satFuncCtrlY;

		switch (_viewAngle) {

		case VerticalViewAngleType.MediumAngle:

			satFuncCtrlX = new List<float> { 0.0f, 90.0f, 180.0f };
			satFuncCtrlY = new List<float> { 0.0f, 1.0f, 0.0f };
			this.satFunction = new CLLinearSplineSatFunction (satFuncCtrlX, satFuncCtrlY);
			break;

		default:
			break;


		}


	}

	
		static VerticalViewAngleType AngleFromString( string _angle ) {
			switch (_angle) {

			case "medium":
				return VerticalViewAngleType.MediumAngle;
				break;
			default:
				return VerticalViewAngleType.MediumAngle; 	

			}
		}



}
