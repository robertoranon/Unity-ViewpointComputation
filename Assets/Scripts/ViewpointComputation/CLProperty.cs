/*
 -----------------------------------------------------------------------------
 This source file is part of ViewpointComputationLib (a viewpoint computation library)
 For more info on the project, contact Roberto Ranon at roberto.ranon@uniud.it.
 
 Copyright (c) 2013- University of Udine, Italy - http://hcilab.uniud.it
 -----------------------------------------------------------------------------

 CLProperty.cs: file defining classes for visual properties and their aggregation

 -----------------------------------------------------------------------------
 */

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// A generic visual property
/// </summary>
public abstract class CLVisualProperty {
	
	/// <summary>
	/// Computes the property satisfaction.
	/// </summary>
	/// <returns>The satisfaction.</returns>
	/// <param name="cameraMan">Camera man.</param>
	/// <param name="threshold">Threshold for lazy evaluation. Defaults to non-lazy evaluation</param>
	public abstract float EvaluateSatisfaction (CLCameraMan cameraMan, float threshold = -0.001f);
	
	/// <summary>
	/// True if the property, in the current evaluation of a viewpoint, has already been evaluated
	/// </summary>
	public bool evaluated;
	
	/// <summary>
	/// The property satisfaction in [0,1] for the current evaluation, stored by the ComputeSatisfaction method.
	/// </summary>
	public float satisfaction;

	/// <summary>
	/// List of targets mentioned by the property.
	/// </summary>
	public List<CLTarget> targets;

	/// <summary>
	/// The name of the property.
	/// </summary>
	public string name;

	/// <summary>
	/// The combined in screen ratio of the property targets
	/// </summary>
	public float inScreenRatio;

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="_name">property name.</param>
	/// <param name="_targets">property targets.</param>
	public CLVisualProperty ( string _name, List<CLTarget> _targets ) {
		name = _name;
		targets = _targets;
	}
		
}

/// <summary>
/// A generic visual property built by aggregating other visual properties.
/// </summary>
public abstract class CLAggregationProperty : CLVisualProperty {

	/// <summary>
	/// List of properties that are aggregated.
	/// </summary>
	public List<CLVisualProperty> properties;

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="_name">property name.</param>
	/// <param name="_targets">property targets.</param>
	/// <param name="_properties">aggregated properties.</param>
	public CLAggregationProperty( string _name, List<CLTarget> _targets, List<CLVisualProperty> _properties ) : base( _name, _targets ) {
		properties = _properties;
	}

	public CLAggregationProperty (string _name) : base (_name, new List<CLTarget> ()){}

}




/// <summary>
/// A weighted sum aggregation of visual properties.
/// </summary>
public class CLTradeOffSatisfaction : CLAggregationProperty {
	
	// List of weights of the aggregated properties;
	public List<float> weights;

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="_name">property name.</param>
	/// <param name="_targets">property targets.</param>
	/// <param name="_properties">aggregated properties.</param>
	/// <param name="_weights">list of property weights.</param>
	public CLTradeOffSatisfaction( string _name, List<CLTarget> _targets, List<CLVisualProperty> _properties, List<float> _weights ) : base( _name, _targets, _properties ) {
		// normalize weights;
		float sum = 0.0f;
		foreach (float w in _weights) {
			sum += w;
		}
		for (int i = 0; i < _weights.Count; i++) {
			_weights [i] = _weights [i] / sum;
		}
		weights = _weights;
	}

	public CLTradeOffSatisfaction (string _name, List<float> _weights) : base (_name){
		// normalize weights;
		float sum = 0.0f;
		foreach (float w in _weights) {
			sum += w;
		}
		for (int i = 0; i < _weights.Count; i++) {
			_weights [i] = _weights [i] / sum;
		}
		weights = _weights;

	}

	// evaluates constraints in the given order in the list using lazy evaluation (so they should be ordered by 
	// increasing evaluation cost)   
	public override float EvaluateSatisfaction( CLCameraMan camera, float threshold ) {
		
		if (!evaluated) {
			inScreenRatio = 1.0f;
			float currentSat = 0.0f;
			float maxSat = 1.0f;
			bool lazyTriggered = false;
			int weightIndex = 0;

			foreach (CLVisualProperty f in this.properties) {
				currentSat += f.EvaluateSatisfaction (camera)*weights[weightIndex];
				inScreenRatio = inScreenRatio*f.inScreenRatio;
				maxSat -= weights [weightIndex++];
				if ((maxSat + currentSat) < threshold) {
					lazyTriggered = true;
					break;
				}
			}
			if (lazyTriggered) {
				currentSat = -1.0f;
			} 

			evaluated = true;
			satisfaction = currentSat;

		}

		return satisfaction;
	}
	
}





/// <summary>
/// A generic ground property, i.e. a property that directly refers to specific targets
/// </summary>
public abstract class CLGroundProperty : CLVisualProperty {
	
	/// sat function
	public CLSatFunction satFunction;

	/// value of the property (computed by ComputeValue)
	public float value;

	public override float EvaluateSatisfaction( CLCameraMan camera, float threshold ) {

		if (! evaluated ) {
			inScreenRatio=RenderTargets (camera);
			value=this.ComputeValue (camera);
			satisfaction = satFunction.ComputeSatisfaction(value) * inScreenRatio;
			evaluated = true;
		}

		return satisfaction;

	}

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="_name">property name.</param>
	/// <param name="_targets">property targets.</param>
	/// <param name="_satFunction">x points of the sat spline</param>
	public CLGroundProperty ( string _name, List<CLTarget> _targets, CLSatFunction _satFunction ) : base (_name, _targets ) {
		satFunction = _satFunction;
	}

	public CLGroundProperty (string _name) : base (_name, new List<CLTarget> ()){}

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="_name">property name.</param>
	/// <param name="_targets">property targets.</param>
	public CLGroundProperty ( string _name, List<CLTarget> _targets ) : base (_name, _targets ) {
	}


	// this is ok but it might not be necessary to render all targets once e.g. we know that one is off screen
	/// <summary>
	/// Renders the targets of the property
	/// </summary>
	/// <returns>The targets.</returns>
	/// <param name="camera">Camera from which to render</param>
	public float RenderTargets(CLCameraMan camera) {
		
		float inScreenRatio = 1.0f;
		foreach (CLTarget t in targets) {
			if (!t.rendered) {
				t.Render (camera);
				t.rendered = true;
			}
			inScreenRatio = t.inScreenRatio * inScreenRatio;
		}
		return inScreenRatio;
		
	}
		
	/// <summary>
	/// Computes the value of the property (e.g., size of the target)
	/// </summary>
	/// <param name='camera'>
	/// Camera.
	/// </param>
	public abstract float ComputeValue (CLCameraMan camera); 
        
	/// <summary>
	/// The cost of evaluating the property. The idea is that properties that are easy to evaluate will have a 1.0 cost, more
	/// complex properties will have a higher value depending on computational cost.
	/// </summary>
	public float cost;
	   
	
	/// <summary>
	/// Generates a random viewpoint position that satisfies the property to some degree, with more probability where satisfaction is higher
	/// </summary>
	/// <returns>The random satisfying position.</returns>
	/// <param name="camera">Camera.</param>
	public abstract Vector3 GenerateRandomSatisfyingPosition (CLCameraMan camera);	 

}


/// <summary>
/// Defines a screen size property. A size property measures the size (area, width, or height) of the screen representation of its first target 
/// as a value in [0,1] where 0 means that the target is not on screen, and 1 is the area / width / height of the second target (if one is specified) 
/// or the viewport (if a second target is not specified.
/// </summary>
public	class CLSizeProperty : CLGroundProperty
{
		

	/// type of size mode
	public enum SizeMode
	{
		AREA,
		WIDTH,
		HEIGHT
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SizeProperty"/> class.
	/// </summary>
	/// <param name="_sizeMode">size mode.</param>
	/// <param name="_name">property name</param>
	/// <param name="_targets">property targets</param>
	/// <param name="_satXPoints">x points of the satisfaction linear spline</param>
	/// <param name="_satYPoints">y points of the satisfaction linear spline</param>
	public CLSizeProperty (SizeMode _sizeMode, string _name, List<CLTarget> _targets, List<float> _satXPoints, List<float> _satYPoints) :
		base ( _name, _targets )
	{
		satFunction = new CLLinearSplineSatFunction (_satXPoints, _satYPoints);
		sizeType = _sizeMode;
		cost = 1.0f;
	}

	public CLSizeProperty (string _name) : base (_name, new List<CLTarget> ()){}

	/// <summary>
	/// Initializes a new instance of the <see cref="SizeProperty"/> class.
	/// </summary>
	/// <param name="_sizeMode">size mode.</param>
	/// <param name="_name">property name</param>
	/// <param name="_targets">property targets</param>
	/// <param name="_satFunction">sat function</param>
	public CLSizeProperty (SizeMode _sizeMode, string _name, List<CLTarget> _targets, CLSatFunction _satFunction) :
	base ( _name, _targets, _satFunction )
	{
		sizeType = _sizeMode;
		cost = 1.0f;
	}
	
		
	/** type of size property, i.e. AREA, WIDTH or HEIGHT */
	public SizeMode sizeType;
        
        
	/// <summary>
	/// Computes the screen size of the property target
	/// </summary>
	/// <returns>The value.</returns>
	/// <param name="camera">Camera.</param>
	public override float ComputeValue (CLCameraMan camera)
	{
		
		if (targets.Count == 1) { // one target, we report size with respect to the viewport
			if (sizeType == SizeMode.AREA)
				return Mathf.Min (targets [0].screenArea, satFunction.domain.y);
        
			if (sizeType == SizeMode.WIDTH)
				return Mathf.Min (targets [0].screenSpaceAABB.CalculateWidth (), satFunction.domain.y);
        
			// else: height property
			return Mathf.Min (targets [0].screenSpaceAABB.CalculateHeight (), satFunction.domain.y);
		} else { // there are two targets, we report size of first with respect to second
			if (sizeType == SizeMode.AREA)
				return Mathf.Min (targets [0].screenArea / targets [1].screenArea, satFunction.domain.y);
        
			if (sizeType == SizeMode.WIDTH)
				return Mathf.Min (targets [0].screenSpaceAABB.CalculateWidth() / targets [1].screenSpaceAABB.CalculateWidth (), satFunction.domain.y);
        
			// else: height property
			return Mathf.Min (targets [0].screenSpaceAABB.CalculateHeight () / targets [1].screenSpaceAABB.CalculateHeight (), satFunction.domain.y);
		}
	}
        
	/// <summary>
	/// Generates a random distance from the target with more probability where satisfaction is higher
	/// </summary>
	/// <returns>The random satisfying distance.</returns>
	/// <param name="camera">Camera.</param>
	public float GenerateRandomSatisfyingDistance( CLCameraMan camera )
	{

		float FOV, result;

		Vector2 yFOVrange = camera.cameraDomain.yFOVBounds;
		float yFOV = (yFOVrange [1] + yFOVrange [0]) / 2;  // required vertical FOV
		// AR is width/height
		float AR = camera.unityCamera.aspect;
		// we compute horizontal FOV
		float xFOV = AR * yFOV;
		if (sizeType == SizeMode.WIDTH) {
			FOV = xFOV;
		} else if (sizeType == SizeMode.HEIGHT) {
			FOV = yFOV;
		} else {
			// AREA property. We convert areas to radiuses, and use the min of the two FOVs
			FOV = Mathf.Min (xFOV, yFOV);
		}

		float bs_radius = targets [0].radius;
		float tmp = 0.3f / Mathf.Tan (Mathf.Deg2Rad * FOV / 2);   // should be 0.5, but bs is bigger than AABB

		float randomSize = satFunction.GenerateRandomXPoint ();
		
		// convert x, which is a size, to a distance from camera
		
		if (randomSize < 0.0001f)
			randomSize = 0.0001f; // to avoid computing an infinite distance.
		
		if (sizeType == SizeMode.AREA) {
			// AREA property. We convert areas to radiuses, and use the average of the two FOVs
			randomSize = Mathf.Sqrt (randomSize / Mathf.PI);
		}
		
		result = (bs_radius / randomSize) * tmp;  // now x is a distance (instead of width, height or area)

		return result;
	}


	/// <summary>
	/// Generates a random viewpoint position that satisfies the property to some degree, with more probability where
	/// satisfaction is higher
	/// </summary>
	/// <returns>The random satisfying position.</returns>
	/// <param name="camera">Camera.</param>
	public override Vector3 GenerateRandomSatisfyingPosition (CLCameraMan camera)
	{
		Vector3 result = new Vector3 ();
    
		bool found = false;
    
		int maxtries = 0;

		while (!found && maxtries<30) {
        
			maxtries++;
			float distance = GenerateRandomSatisfyingDistance ( camera 
			                                                   );

			// check we are outside bs. we don't want to assign candidates inside bs.
			if (distance > targets [0].radius) {
				// generate random direction
				float inclination = (Mathf.PI) * Random.value;
				float azimuth = Mathf.PI * 2 * Random.value;
            
				result.x = distance * Mathf.Sin (azimuth) * Mathf.Sin (inclination);
				result.y = distance * Mathf.Cos (inclination);
				result.z = distance * Mathf.Cos (azimuth) * Mathf.Sin (inclination);
            
				result = result + targets [0].targetAABB.center;
            
				if (camera.InSearchSpace( new float[] {result.x, result.y, result.z} )) {
					found = true;
				}
			}
		}

		if (!found)
		{
			float[] randomCandidate = camera.ComputeRandomViewpoint(3);
			result = new Vector3( randomCandidate[0], randomCandidate[1], randomCandidate[2]);
		}

		return result;
		
	}
        
}


/// <summary>
/// Defines an occlusion property. An occlusion property measures how much a target is occluded, where 0 = no occlusion, and 1=full occlusion
/// </summary>
public class CLOcclusionProperty : CLGroundProperty
{
	
	// if false, ray cast just from camera to targets (i.e. towards front faces); if true, we perform double ray casts by also
	// shooting from target to camera (i.e. each ray is in both ways)
	bool frontBack; 

	// if true, ray casts are towards random points in target bounds; if false, we use custom fixed positions inside AABB
	bool randomRayCasts;

	/// <summary>
	/// Initializes a new instance of the <see cref="OcclusionProperty"/> class.
	/// </summary>
	/// <param name="_name">property name</param>
	/// <param name="_targets">property targets</param>
	/// <param name="_satXPoints">x points of the satisfaction linear spline</param>
	/// <param name="_satYPoints">y points of the satisfaction linear spline</param>
	public CLOcclusionProperty (string _name, List<CLTarget> _targets, List<float> _satXPoints, List<float> _satYPoints, bool _frontBack = true, bool _randomRayCasts = false) :
		base ( _name, _targets )
	{
		satFunction = new CLLinearSplineSatFunction (_satXPoints, _satYPoints);
		cost = 20.0f;
		frontBack = _frontBack;
		randomRayCasts = _randomRayCasts;
	}

	public CLOcclusionProperty (string _name) : base (_name, new List<CLTarget> ()){}

	/// <summary>
	/// Initializes a new instance of the <see cref="OcclusionProperty"/> class.
	/// </summary>
	/// <param name="_name">property name</param>
	/// <param name="_targets">property targets</param>
	/// <param name="_satFunction">sat function</param>
	public CLOcclusionProperty (string _name, List<CLTarget> _targets, CLSatFunction _satFunction, bool _frontBack = true, bool _randomRayCasts = false ) :
	base ( _name, _targets, _satFunction )
	{
		cost = 20.0f;
		frontBack = _frontBack;
		randomRayCasts = _randomRayCasts;

	}
	

	/// <summary>
	/// Computes the degree of occlusion
	/// </summary>
	/// <returns>The value.</returns>
	/// <param name="camera">Camera.</param>
	public override float ComputeValue (CLCameraMan camera)
	{
		return Mathf.Min (targets [0].ComputeOcclusion (camera, frontBack, randomRayCasts), satFunction.domain.y);
	}
        

	/// <summary>
	/// Generates a random viewpoint position that satisfies the property to some degree, with more probability where
	/// satisfaction is higher
	/// </summary>
	/// <returns>The random satisfying position.</returns>
	/// <param name="camera">Camera.</param>
	public override Vector3 GenerateRandomSatisfyingPosition (CLCameraMan camera)
	{
		return new Vector3 (0.0f, 0.0f, 0.0f);
	}
		

}

/// <summary>
/// Camera orientation property. Controls how much camera orientation is close to some reference orientation.
/// </summary>
public class CLCameraOrientationProperty : CLGroundProperty
{
	
	/// <summary>
	/// The reference camera orientation.
	/// </summary>
	private Quaternion referenceCameraOrientation;


	/// <summary>
	/// Initializes a new instance of the <see cref="CameraOrientationProperty"/> class.
	/// </summary>
	/// <param name="cameraOrientation">reference orientation.</param>
	/// <param name="_name">property name</param>
	/// <param name="_satXPoints">x points of the satisfaction linear spline</param>
	/// <param name="_satYPoints">y points of the satisfaction linear spline</param>
	public CLCameraOrientationProperty (Quaternion cameraOrientation, string _name, List<float> _satXPoints, List<float> _satYPoints) :
	base ( _name, new List<CLTarget>() )
	{
		satFunction = new CLLinearSplineSatFunction (_satXPoints, _satYPoints);
		cost = 1.0f;
		referenceCameraOrientation = new Quaternion (cameraOrientation.x, cameraOrientation.y, cameraOrientation.z, cameraOrientation.w);
	}

	public CLCameraOrientationProperty (string _name) : base (_name, new List<CLTarget> ()){}

	/// <summary>
	/// Initializes a new instance of the <see cref="CameraOrientationProperty"/> class.
	/// </summary>
	/// <param name="cameraOrientation">reference orientation.</param>
	/// <param name="_name">property name</param>
	/// <param name="_satFunction">sat function</param>
	public CLCameraOrientationProperty (Quaternion cameraOrientation, string _name, CLSatFunction _satFunction) :
	base ( _name, new List<CLTarget>(), _satFunction )
	{
		cost = 1.0f;
		referenceCameraOrientation = new Quaternion (cameraOrientation.x, cameraOrientation.y, cameraOrientation.z, cameraOrientation.w);
	}
	

	/// <summary>
	/// Computes the value of the property. In this case, the angle between the provided reference camera rotation, and the rotation of the camera
	/// </summary>
	/// <returns>The computed angle</returns>
	/// <param name="camera">Camera.</param>
	public override float ComputeValue (CLCameraMan camera)
	{
		return Quaternion.Angle (referenceCameraOrientation, camera.unityCamera.transform.rotation);
	}
	

	/// <summary>
	/// Generates a random viewpoint position that satisfies the property to some degree, with more probability where
	/// satisfaction is higher
	/// </summary>
	/// <returns>The random satisfying position.</returns>
	/// <param name="camera">Camera.</param>
	public override Vector3 GenerateRandomSatisfyingPosition (CLCameraMan camera)
	{
		return new Vector3 (0.0f, 0.0f, 0.0f);
	}
	
}


/// <summary>
/// Camera FOV property.
/// </summary>
public class CLCameraFOVProperty : CLGroundProperty
{


	/// <summary>
	/// Initializes a new instance of the <see cref="CLCameraFOVProperty"/> class.
	/// </summary>
	/// <param name="_name">property name</param>
	/// <param name="_satXPoints">x points of the satisfaction linear spline</param>
	/// <param name="_satYPoints">y points of the satisfaction linear spline</param>
	public CLCameraFOVProperty (string _name, List<float> _satXPoints, List<float> _satYPoints) :
	base ( _name, new List<CLTarget>() )
	{
		satFunction = new CLLinearSplineSatFunction (_satXPoints, _satYPoints);
		cost = 1.0f;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CLCameraFOVProperty"/> class.
	/// </summary>
	/// <param name="_name">property name</param>
	/// <param name="_satFunction">sat function</param>
	public CLCameraFOVProperty (string _name, CLSatFunction _satFunction) :
	base ( _name, new List<CLTarget>(), _satFunction )
	{
		cost = 1.0f;
	}

	public CLCameraFOVProperty (string _name) : base (_name, new List<CLTarget> ()){}


	/// <summary>
	/// Computes the value of the property. In this case, the YFOV of the provided camera
	/// </summary>
	/// <returns>The computed angle</returns>
	/// <param name="camera">Camera.</param>
	public override float ComputeValue (CLCameraMan camera)
	{
		
		return Mathf.Min (camera.unityCamera.fieldOfView, satFunction.domain.y);
	}


	/// <summary>
	/// Generates a random viewpoint position that satisfies the property to some degree, with more probability where
	/// satisfaction is higher
	/// </summary>
	/// <returns>The random satisfying position.</returns>
	/// <param name="camera">Camera.</param>
	public override Vector3 GenerateRandomSatisfyingPosition (CLCameraMan camera)
	{
		return new Vector3 (0.0f, 0.0f, 0.0f);
	}

}
	
/// <summary>
/// A target position property measures how much the center of a target is far from a provided position on viewport
/// </summary>
public class CLTargetPositionProperty : CLGroundProperty
{


	/// <summary>
	/// Initializes a new instance of the <see cref="FramingProperty"/> class.
	/// </summary>
	/// <param name="_position">position on viewport (normalized in [0,1] in both dimensions)</param>
	/// <param name="_name">property name</param>
	/// <param name="_targets">property targets. They have no meaning for this property, but we add them anyway.</param>
	/// <param name="_satXPoints">x points of the satisfaction linear spline (distances from position)</param>
	/// <param name="_satYPoints">y points of the satisfaction linear spline</param>
	public CLTargetPositionProperty (string _name, List<CLTarget> _targets, Vector2 _position, List<float> _satXPoints, List<float> _satYPoints) :
	base ( _name, _targets )
	{
		satFunction = new CLLinearSplineSatFunction (_satXPoints, _satYPoints);
		cost = 1.0f;
		position = _position;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FramingProperty"/> class.
	/// </summary>
	/// <param name="_position">position on viewport (normalized in [0,1] in both dimensions)</param>
	/// <param name="_name">property name</param>
	/// <param name="_targets">property targets. They have no meaning for this property, but we add them anyway.</param>
	/// <param name="_satFunction">sat function</param>
	public CLTargetPositionProperty (string _name, List<CLTarget> _targets, Vector2 _position, CLSatFunction _satFunction) :
	base ( _name, _targets, _satFunction )
	{
		cost = 1.0f;
		position = _position;
	}

	public CLTargetPositionProperty (string _name) : base (_name, new List<CLTarget> ()){}


	/// <summary>
	/// Computes how much screen representation is inside given rectangular frame
	/// </summary>
	/// <returns>The value.</returns>
	/// <param name="camera">Camera.</param>
	public override float ComputeValue (CLCameraMan camera)
	{
		// compute projection of target center
		Vector3 projectedCenter = camera.unityCamera.WorldToViewportPoint (targets[0].targetAABB.center);
		// compute distance from desidered point
		float distance = (position - new Vector2( projectedCenter[0], projectedCenter[1])).magnitude;

		return Mathf.Min (distance, satFunction.domain.y);
	}


	/// <summary>
	/// Generates a random viewpoint position that satisfies the property to some degree, with more probability where
	/// satisfaction is higher
	/// </summary>
	/// <returns>The random satisfying position.</returns>
	/// <param name="camera">Camera.</param>
	public override Vector3 GenerateRandomSatisfyingPosition (CLCameraMan camera)
	{
		return new Vector3 (0.0f, 0.0f, 0.0f);
	}

	/** frame with each dimension in [0,1] (0=bottom left corner of the viewport) */
	public Vector2 position;


}

    
/// <summary>
/// A framing property measures how much the screen representation of its target is inside a given rectangular frame, as a value 
/// in [0,1] where 0 means totally outside and 1 totally inside.
/// </summary>
public class CLFramingProperty : CLGroundProperty
{
		

	/// <summary>
	/// Initializes a new instance of the <see cref="FramingProperty"/> class.
	/// </summary>
	/// <param name="_rect">rectangular frame</param>
	/// <param name="_name">property name</param>
	/// <param name="_targets">property targets. They have no meaning for this property, but we add them anyway.</param>
	/// <param name="_satXPoints">x points of the satisfaction linear spline</param>
	/// <param name="_satYPoints">y points of the satisfaction linear spline</param>
	public CLFramingProperty (Rectangle _rect, string _name, List<CLTarget> _targets, List<float> _satXPoints, List<float> _satYPoints) :
		base ( _name, _targets )
	{
		satFunction = new CLLinearSplineSatFunction (_satXPoints, _satYPoints);
		cost = 1.0f;
		frame = _rect;
	}	

	/// <summary>
	/// Initializes a new instance of the <see cref="FramingProperty"/> class.
	/// </summary>
	/// <param name="_rect">rectangular frame</param>
	/// <param name="_name">property name</param>
	/// <param name="_targets">property targets. They have no meaning for this property, but we add them anyway.</param>
	/// <param name="_satFunction">sat function</param>
	public CLFramingProperty (Rectangle _rect, string _name, List<CLTarget> _targets, CLSatFunction _satFunction) :
	base ( _name, _targets, _satFunction )
	{
		cost = 1.0f;
		frame = _rect;
	}

	public CLFramingProperty (string _name) : base (_name, new List<CLTarget> ()){}
	

	/// <summary>
	/// Computes how much screen representation is inside given rectangular frame
	/// </summary>
	/// <returns>The value.</returns>
	/// <param name="camera">Camera.</param>
	public override float ComputeValue (CLCameraMan camera)
	{
		return Mathf.Min (targets [0].ComputeRatioInsideFrame (camera, frame), satFunction.domain.y);
	}
        

	/// <summary>
	/// Generates a random viewpoint position that satisfies the property to some degree, with more probability where
	/// satisfaction is higher
	/// </summary>
	/// <returns>The random satisfying position.</returns>
	/// <param name="camera">Camera.</param>
	public override Vector3 GenerateRandomSatisfyingPosition (CLCameraMan camera)
	{
		return new Vector3 (0.0f, 0.0f, 0.0f);
	}
		
	/** frame with each dimension in [0,1] (0=bottom left corner of the viewport) */
	public Rectangle frame;
		
		
}
	
    
/// <summary>
/// An orientation property has three modes. In the HORIZONTAL mode, it measures the angle (in degrees, in the [-180,180] range) 
/// between the front (local +Z) vector of the target and the projection in the local XZ plane of the target of the vector from the target to the camera. 
/// In the VERTICAL mode, it measures the angle (in degrees, in the [0,180] range) between the up (local +Y) vector of the target and the vector from the target to the camera.
/// In the VERTICAL_WORLD mode, it measures the angle (in degrees, in the [0,180] range) between the up vector of the world and the vector from the target to the camera.
/// </summary>
public class CLOrientationProperty : CLGroundProperty
{
		
	/// type of orientation moed
	public enum OrientationMode
	{
		VERTICAL,    // in the local basis of the object
		HORIZONTAL,  // in the local basis of the object
		VERTICAL_WORLD  // in the global basis
	}
        
	/// <summary>
	/// Initializes a new instance of the <see cref="OrientationProperty"/> class.
	/// </summary>
	/// <param name="mode">Mode.</param>
	// <param name="_name">property name</param>
	/// <param name="_targets">property targets. They have no meaning for this property, but we add them anyway.</param>
	/// <param name="_satXPoints">x points of the satisfaction linear spline</param>
	/// <param name="_satYPoints">y points of the satisfaction linear spline</param>
	public CLOrientationProperty (OrientationMode mode, string _name, List<CLTarget> _targets, List<float> _satXPoints, List<float> _satYPoints) :
		base ( _name, _targets )
	{
		satFunction = new CLLinearSplineSatFunction (_satXPoints, _satYPoints);
		cost = 1.5f;
		orientation = mode;
	}	

	/// <summary>
	/// Initializes a new instance of the <see cref="OrientationProperty"/> class.
	/// </summary>
	/// <param name="mode">Mode.</param>
	// <param name="_name">property name</param>
	/// <param name="_targets">property targets. They have no meaning for this property, but we add them anyway.</param>
	/// <param name="_satFunction">sat function</param>
	public CLOrientationProperty (OrientationMode mode, string _name, List<CLTarget> _targets, CLSatFunction _satFunction) :
	base ( _name, _targets, _satFunction )
	{
		cost = 1.5f;
		orientation = mode;
	}	

	public CLOrientationProperty (string _name) : base (_name, new List<CLTarget> ()){}
	
	/// <summary>
	/// Computes the value of the property (depending on the actual mode)
	/// </summary>
	/// <returns>The value.</returns>
	/// <param name="camera">Camera.</param>
	public override float ComputeValue (CLCameraMan camera)
	{
		Vector3 viewTarget;
		// world target to camera
		Vector3 targetToCamera = (camera.unityCamera.transform.position - targets [0].targetAABB.center).normalized;
		// we must now convert it to 
		
		float angle;
	
		if (orientation == OrientationMode.HORIZONTAL) {
			// retrieve local up vector for the target
			Vector3 up = targets [0].gameObjectRef.transform.up.normalized;
		
			// up is the normal of the horizontal plane of the target
			// we project targetToCamera to the horizontal plane using
			// v1_projected = v1 - Dot(v1, n) * n;
			viewTarget = (targetToCamera - Vector3.Dot (targetToCamera, up) * up).normalized;

			angle = targets [0].ComputeAngleWith (viewTarget, TargetUtils.Axis.FORWARD);
		} else if (orientation == OrientationMode.VERTICAL) {
			viewTarget = targetToCamera;
			angle = targets [0].ComputeAngleWith (viewTarget, TargetUtils.Axis.UP);
		} else {  // orientation == OrientationMode.VERTICAL_WORLD)
			viewTarget = targetToCamera;
			angle = targets [0].ComputeAngleWith(viewTarget, TargetUtils.Axis.WORLD_UP);
		}
	
		return Mathf.Min (angle, satFunction.domain.y);	
		
		
	}
        

	/// <summary>
	/// Generates a random viewpoint position that satisfies the property to some degree, with more probability where
	/// satisfaction is higher
	/// </summary>
	/// <returns>The random satisfying position.</returns>
	/// <param name="camera">Camera.</param>
	public override Vector3 GenerateRandomSatisfyingPosition (CLCameraMan camera)
	{
		
		Vector3 result = new Vector3 ();
    
		bool found = false;
    
		Vector2 minMaxDistance = new Vector2 (0.1f, 1000f);  // just a placeholder; this method should not be used anymore
    
		int maxtries = 0;
		while (!found && maxtries<30) {
        
			maxtries	++;
			float x = satFunction.GenerateRandomXPoint (); // random angle
			//Debug.Log (x);
			//x = 0.0f;
			// generate random distance
			float distance = minMaxDistance.x + Random.value * (minMaxDistance.y - minMaxDistance.x);
			// generate random height
			float height = camera.ComputeRandomViewpoint(3)[1];
			if (orientation == OrientationMode.HORIZONTAL) {
				result.x = Mathf.Sin (Mathf.Deg2Rad * x) * distance;
				result.z = Mathf.Cos (Mathf.Deg2Rad * x) * distance;
				result = targets [0].gameObjectRef.transform.TransformPoint (result);
				result.y = height;
			} else {  // VERTICAL
				// generate a random theta in cilindrical coordinates
				float theta = Random.value * 2 * Mathf.PI;
				float phi = Mathf.Deg2Rad * x;
            
				// convert from spherical to cartesian
				result.x = Mathf.Sin (phi) * Mathf.Cos (theta) * distance;
				result.y = Mathf.Sin (phi) * Mathf.Sin (theta) * distance;
				result.z = Mathf.Cos (phi) * distance;
            
				result = targets [0].gameObjectRef.transform.TransformPoint (result);
			}
        
			// if we are in problem bounds, ok - otherwise we throw away the point and generate a new one
			if (camera.InSearchSpace( new float[] {result.x, result.y, result.z})) {
				found = true;
			}
        
		}
    

		if (!found)
		{
			float[] randomCandidate = camera.ComputeRandomViewpoint(3);
			result = new Vector3( randomCandidate[0], randomCandidate[1], randomCandidate[2]);
		}
		
		return result;

		
	}
        
	/// <summary>
	/// The orientation mode
	/// </summary>
	public OrientationMode orientation;
		
}
	
    
/// <summary>
/// A Relative Position property measures how much the two targets satisfy the relation. For example, in the case positionType=LEFT, it measures the percentange 
/// of the first target' screen representation that is left of any point of the second target screen representation. Remark: FRONT and BEHIND are not implemented
/// </summary>
public class CLRelativePositionProperty : CLGroundProperty
{
		

	/// relative position mode
	public enum RelativePositionMode
	{
		LEFT,
		RIGHT,
		ABOVE,
		BELOW,
		FRONT,
		BEHIND
	}
	

	/// <summary>
	/// Initializes a new instance of the <see cref="RelativePositionProperty"/> class.
	/// </summary>
	/// <param name="mode">Mode.</param>
	// <param name="_name">property name</param>
	/// <param name="_targets">property targets. They have no meaning for this property, but we add them anyway.</param>
	/// <param name="_satXPoints">x points of the satisfaction linear spline</param>
	/// <param name="_satYPoints">y points of the satisfaction linear spline</param>
	public CLRelativePositionProperty (RelativePositionMode mode, string _name, List<CLTarget> _targets, List<float> _satXPoints, List<float> _satYPoints) :
		base ( _name, _targets )
	{
		satFunction = new CLLinearSplineSatFunction (_satXPoints, _satYPoints);
		cost = 1.5f;
		positionType = mode;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RelativePositionProperty"/> class.
	/// </summary>
	/// <param name="mode">Mode.</param>
	// <param name="_name">property name</param>
	/// <param name="_targets">property targets. They have no meaning for this property, but we add them anyway.</param>
	/// <param name="_satFunction">sat function</param>
	public CLRelativePositionProperty (RelativePositionMode mode, string _name, List<CLTarget> _targets, CLSatFunction _satFunction) :
	base ( _name, _targets, _satFunction )
	{
		cost = 1.5f;
		positionType = mode;
	}

	public CLRelativePositionProperty (string _name) : base (_name, new List<CLTarget> ()){}
        
	/// <summary>
	/// Computes the value of the property 
	/// </summary>
	/// <returns>The value.</returns>
	/// <param name="camera">Camera.</param>
	public override float ComputeValue (CLCameraMan camera)
	{
		
		if (positionType == RelativePositionMode.LEFT)
			return targets [0].ComputeRatioInsideFrame (camera, new Rectangle (0.0f, targets [1].screenSpaceAABB.xMin, 0.0f, 1.0f));
		
		if (positionType == RelativePositionMode.BELOW)
			return targets [0].ComputeRatioInsideFrame (camera, new Rectangle (0.0f, 1.0f, 0.0f, targets [1].screenSpaceAABB.yMin));
		
		if (positionType == RelativePositionMode.RIGHT)
			return targets [0].ComputeRatioInsideFrame (camera, new Rectangle (targets [1].screenSpaceAABB.xMax, 1.0f, 0.0f, 1.0f));
			
		// else: ABOVE property
		return targets [0].ComputeRatioInsideFrame (camera, new Rectangle (0.0f, 1.0f, targets [1].screenSpaceAABB.yMax, 1.0f));
		
	}
        
	/// <summary>
	/// Generates a random viewpoint position that satisfies the property to some degree, with more probability where
	/// satisfaction is higher
	/// </summary>
	/// <returns>The random satisfying position.</returns>
	/// <param name="camera">Camera.</param>
	public override Vector3 GenerateRandomSatisfyingPosition (CLCameraMan camera)
	{
		return new Vector3 (0.0f, 0.0f, 0.0f);
	}
        
		
	/// <summary>
	/// The mode of the relative position property.
	/// </summary>
	RelativePositionMode positionType;
		
}


/// <summary>
/// Debug property, always evaluates to 0
/// </summary>
public class CLDebugProperty : CLGroundProperty
{


	/// <summary>
	/// Initializes a new instance of the <see cref="CLCameraFOVProperty"/> class.
	/// </summary>
	/// <param name="_name">property name</param>
	public CLDebugProperty (string _name) :
	base ( _name, new List<CLTarget>() )
	{
		cost = 1.0f;
	}


	/// <summary>
	/// Computes the value of the property. In this case, the YFOV of the provided camera
	/// </summary>
	/// <returns>The computed angle</returns>
	/// <param name="camera">Camera.</param>
	public override float ComputeValue (CLCameraMan camera)
	{

		return 0.0f;
	}


	/// <summary>
	/// Generates a random viewpoint position that satisfies the property to some degree, with more probability where
	/// satisfaction is higher
	/// </summary>
	/// <returns>The random satisfying position.</returns>
	/// <param name="camera">Camera.</param>
	public override Vector3 GenerateRandomSatisfyingPosition (CLCameraMan camera)
	{
		return new Vector3 (0.0f, 0.0f, 0.0f);
	}

}




