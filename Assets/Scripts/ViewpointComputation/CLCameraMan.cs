/*
-----------------------------------------------------------------------------
This source file is part of ViewpointComputationLib (a viewpoint computation library)
For more info on the project, contact Roberto Ranon at roberto.ranon@uniud.it.

Copyright (c) 2013- University of Udine, Italy - http://hcilab.uniud.it
-----------------------------------------------------------------------------

 CLCameraMan.cs: file defining classes for representing a camera man, i.e. an object that is 
 able to evaluate how much a specific unity camera satisfies a VC problem specification (satisfaction function + 
 camera parameter bounds).

-----------------------------------------------------------------------------
*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;


/// <summary>
/// Abstract class defining the camera man. Subclasses then define specific camera men based on the representation
/// used for the camera parameters (e.g. position + lookAt point, or spherical coordinates, or else).
/// </summary> 
public abstract class CLCameraMan
{
	
	/// <summary>
	/// Associated unity camera (for projections, etc.)
	/// </summary>
	public Camera unityCamera;

	/// <summary>
	/// The camera parameters domain, instantiate depending on the specific camera representation.
	/// </summary>
	public VCProblemDomain cameraDomain;
	
	/// <summary>
	/// List of visual properties, the first one is the objective function
	/// </summary>
	public List<CLVisualProperty> properties = new List<CLVisualProperty>();
	
	/// <summary>
	/// List of targets mentioned by the properties
	/// </summary>
	public List<CLTarget> targets = new List<CLTarget>();

	/// <summary>
	/// a special CLTarget, which contains all targets renderables, colliders, and properties
	/// </summary>
	public CLTarget allTargets = new CLTarget();
		
	/// <summary>
	/// Clip rectangle of the camera (defaults to entire viewport).
	/// </summary>
	public Rectangle clipRectangle = new Rectangle (0.0f, 1.0f, 0.0f, 1.0f);
	
	/// <summary>
	/// Sets the VC visual properties.
	/// </summary>
	/// <param name='propertyList'>List of visual properties. The first one is the problem objective function.</param>
	public void SetSpecification (List<CLVisualProperty> propertyList)
	{

		properties.Clear();
		targets.Clear();
		targets.AddRange (propertyList[0].targets);
		properties.AddRange (propertyList);

		foreach (CLTarget t in targets) {
			t.targetProperties.Clear();
		}
			
		// finds associations between targets and ground properties
		foreach (CLVisualProperty p in propertyList) {

			if ( p is CLGroundProperty ) {
				CLGroundProperty gp = (CLGroundProperty) p;
				foreach ( CLTarget t in gp.targets ) {
					t.targetProperties.Add ( gp );
				}
			}
		}
	}

	/// <summary>
	/// Sets camera parameters using the provided parameters, and computes and returns the satisfaction of the objective function.
	/// </summary>
	/// <returns>The satisfaction.</returns>
	/// <param name="cameraParams">Camera parameters.</param>
	/// <param name="lazyThreshold">Lazy threshold. Evaluation stops and returns -1 when it realizes it cannot reach lazyThreshold.</param>
	public float EvaluateSatisfaction (float[] cameraParams, float lazyThreshold) {
		this.updateCamera (cameraParams);
		return EvaluateSatisfaction (lazyThreshold);

	}

	/// <summary>
	/// Computes and returns the satisfaction of the objective function.
	/// </summary>
	/// <returns>The satisfaction.</returns>
	/// <param name="lazyThreshold">Lazy threshold. Evaluation stops and returns -1 when it realizes it cannot reach lazyThreshold.</param>
	public float EvaluateSatisfaction (float lazyThreshold=-0.01f) {
		foreach (CLVisualProperty p in properties) {
			p.evaluated = false;
		}
		foreach (CLTarget t in targets) {
			t.rendered = false;
		}
		// performs recursive evaluation of all properties 
		return properties[0].EvaluateSatisfaction( this, lazyThreshold );
	}
		
	/// <summary>
	/// Updates the camera with the provided parameters
	/// </summary>
	/// <param name="cameraParams">Camera parameters.</param>
	public abstract void updateCamera (float[] cameraParams);


	/// <summary>
	/// Updates the properties targets, computing and setting each target AABB, and returning the AABB of all targets.
	/// </summary>
	/// <returns>The AABB of the targets AABBs</returns>
	public Bounds UpdateTargets ()
	{
		
		Bounds startingAABB = targets [0].UpdateBounds ();
		Bounds allBounds = new Bounds (startingAABB.center, startingAABB.size);


		foreach (CLTarget t in targets.Skip(1 )) {
			allBounds.Encapsulate( t.UpdateBounds() );

		}
		allTargets.targetAABB = allBounds;
		return allBounds;
	}

	/// <summary>
	/// checks if the provided viewpoint is in the search space
	/// </summary>
	/// <returns><c>true</c>, if it is in search space, <c>false</c> otherwise.</returns>
	/// <param name="cameraParams">Viewpoint parameters</param>
	public  bool InSearchSpace (float[] cameraParams) {
		return cameraDomain.InSearchSpace (cameraParams);
	}
	
	/// <summary>
	/// Returns a random viewpoint in search space, expressed as parameters (the value depends on the specific CLCameraMan subclass).
	/// If smart = true, the viewpoint is randomly computed according to the current properties (i.e. with more probability where
	/// the properties are more likely to be satisfied)
	/// </summary>
	/// <param name="dimension">dimension of the viewpoint to be generated</param>
	/// <param name="smart">whether to use smart initialization or not</param>
	/// <param name="t">Target provided for smart initialization or not. If it is provided, we initialize according to
	/// that target properties. If it is null, we instead initialize according to all targets </param>
	/// <returns>The candidate.</returns>
	public abstract float[] ComputeRandomViewpoint ( int dimension, bool smart = false, CLTarget t = null);
		
	/// <summary>
	/// Returns the range between each camera parameter. The meaning of parameters depend on the specific CLCameraMan subclass */
	/// </summary>
	/// <param name="dimension">candidate dimension</param>
	public float[] GetParametersRange (int dimension) { 
		return cameraDomain.GetParametersRange (dimension);
	}

	/// <summary>
	/// Returns the minimum value of each camera parameter. The meaning of parameters depend on the specific CLCameraMan subclass */
	/// </summary>
	/// <param name="dimension">candidate dimension</param>
	public float[] GetMinCameraParameters(int dimension) {
		return cameraDomain.GetMinParameters (dimension);
	}

	/// <summary>
	/// Returns the maximum value of each camera parameter. The meaning of parameters depend on the specific CLCameraMan subclass */
	/// </summary>
	/// <param name="dimension">candidate dimension</param>
	public float[] GetMaxCameraParameters(int dimension) {
		return cameraDomain.GetMaxParameters (dimension);
	}


	/// <summary>
	/// Computes the YFOV from focal lenght.
	/// </summary>
	/// <returns>The YFOV from focal lenght.</returns>
	/// <param name="filmWidthInMM">Film width in millimiters (e.g. 35 mm).</param>
	/// <param name="focalLenght">Focal lenght.</param>
	public float ComputeYFOVFromFocalLenght( float focalLength, float filmWidthInMM = 35.0f ) {
	
		float fovX = 2.0f * Mathf.Atan2(0.5f * filmWidthInMM, focalLength);
		float fovY =  2.0f * Mathf.Atan( Mathf.Tan( fovX / 2.0f ) / unityCamera.aspect );
		return Mathf.Rad2Deg * fovY;

	}

}


/// <summary>
/// A CLookAtCameraMan uses position, look at point, roll and YFOV to represent the camera configuration.
/// </summary>
public class CLLookAtCameraMan : CLCameraMan 
{


	/// <summary>
	/// Blocks camera orientation to the one that the internal camera has initially
	/// </summary>
	public bool blockOrientation = false;

	/// <summary>
	/// Updates the camera with the provided parameters
	/// </summary>
	/// <param name='parameters'>
	/// array of 8 floats: position.x, position.y, position.z, lookAtPoint.x, lookAtPoint.y, lookAtPoint.z, roll angle, vertical fov angle (degrees)
	/// </param>
	public override void updateCamera (float[] parameters)
	{
		this.unityCamera.transform.position = new Vector3 (parameters [0], parameters [1], parameters [2]);
		//this.unityCamera.transform.rotation = Quaternion.identity;
		if (!blockOrientation)
			this.unityCamera.transform.LookAt (new Vector3 (parameters [3], parameters [4], parameters [5]));
		//this.unityCamera.ResetWorldToCameraMatrix();
		if (parameters.Length > 6) {
			if (! Mathf.Approximately (parameters [6], 0.0f))
				unityCamera.transform.Rotate (parameters [6], 0.0f, 0.0f); 
		}
		// vertical FOV in degrees
		if ( parameters.Length > 7 )
			unityCamera.fieldOfView = parameters[7];
	}


	/// <summary>
	/// Returns a random viewpoint in search space
	/// </summary>
	/// <returns>The viewpoint, expressed as an array of float parameters</returns>
	/// <param name="dimension">viewpoint dimension (3 for just position, 6 for position + lookAt, etc)</param>
	/// <param name="smart">whether to use smart random generation or not</param>
	/// <param name="t">target to use in case of smart initialization</param>
	public override float[] ComputeRandomViewpoint ( int dimension, bool smart = false, CLTarget t = null)
	{ 
		float[] candidatePos = cameraDomain.ComputeRandomViewpoint (dimension);

		if (smart) {

			// in this case we use some of the provided target properties to control randomness, i.e. size and orientation, if present. 
			// Look-at point is set randomly inside target AABB. If a target is not provided, we use all targets as a target
			// XXX Warning: we are not handling cases where e.g. the search space is only one or two-dimensional ...

			bool useOrientation = true;
			if ( t==null ) {

				t = allTargets;
				useOrientation = false;
			}
			// this will give up after n tries, so we default to random initialization if there are problems
			Vector3 pos = t.GenerateRandomSatisfyingPosition ( this, useOrientation ); 

			candidatePos [0] = pos.x;	
			candidatePos [1] = pos.y;
			candidatePos [2] = pos.z;

			candidatePos [3] = UnityEngine.Random.Range( t.targetAABB.min.x, t.targetAABB.max.x );
			candidatePos [4] = UnityEngine.Random.Range( t.targetAABB.min.y, t.targetAABB.max.y );
			candidatePos [5] = UnityEngine.Random.Range( t.targetAABB.min.z, t.targetAABB.max.z );
		}

		return candidatePos;
		
	}

}

/// <summary>
/// A CLOrbitCameraMan uses pivot, distance, theta, phi, roll and YFOV to represent the camera configuration.
/// </summary>
public class CLOrbitCameraMan : CLCameraMan 
{
	
	/// <summary>
	/// pivot of the Camera
	/// </summary>
	public Vector3 pivot;
	
	/// <summary>
	/// Updates the camera with the provided parameters
	/// </summary>
	/// <param name='parameters'>
	/// array of 5 floats: distance, theta, phi, roll angle, vertical fov angle (degrees)
	/// </param>
	public override void updateCamera (float[] parameters)
	{
		float x = parameters[0] * Mathf.Sin (Mathf.Deg2Rad*parameters[1]) * Mathf.Cos (Mathf.Deg2Rad*parameters[2]);
		float z = -parameters[0] * Mathf.Sin (Mathf.Deg2Rad*parameters[1]) * Mathf.Sin (Mathf.Deg2Rad*parameters[2]);
		float y = parameters[0] * Mathf.Cos (Mathf.Deg2Rad*parameters[1]);
		this.unityCamera.transform.position = new Vector3 (x,y,z) + pivot;
		//this.unityCamera.transform.rotation = Quaternion.identity;
		this.unityCamera.transform.LookAt (pivot);
		//this.unityCamera.ResetWorldToCameraMatrix();
		if ( ! Mathf.Approximately(parameters[3], 0.0f ))
			unityCamera.transform.Rotate( parameters[3], 0.0f, 0.0f ); 
		// vertical FOV in degrees
		unityCamera.fieldOfView = parameters[4];
	}

	/// <summary>
	/// Returns a random viewpoint in search space
	/// </summary>
	/// <returns>The viewpoint, expressed as an array of float parameters</returns>
	/// <param name="dimension">viewpoint dimension (3 for just position, 6 for position + lookAt, etc)</param>
	/// <param name="smart">whether to use smart random generation or not</param>
	/// <param name="t">target to use in case of smart initialization</param>
	public override float[] ComputeRandomViewpoint ( int dimension, bool smart = false, CLTarget t = null)
	{ 
		float[] candidatePos = cameraDomain.ComputeRandomViewpoint (dimension);
		return candidatePos;
		
	}

}



/// <summary>
/// Abstract class defining the domain of a VC problem. Subclasses then define specific representations
/// depending on how we represent camera parameters (e.g. position + lookAt point, or spherical coordinates, or else).
/// </summary> 
public abstract class VCProblemDomain
{
	/// <summary>
	/// Camera YFOV bounds
	/// </summary>
	public Vector2 yFOVBounds;

	/// <summary>
	/// Camera roll bounds of the VC problem
	/// </summary>
	public Vector2 rollBounds;

	/// <summary>
	/// Minimum distance the camera should have wrt any geometry (any collider). If equal to 0.0, no check is performed.
	/// </summary>
	public float minGeometryDistance = 0.0f;

	/// <summary>
	/// bitmask of Unity layers to be excluded when checking minimum geometry distance.
	/// </summary>
	public int layersToExclude;

	/// <summary>
	/// checks if the provided viewpoint is in the problem domain
	/// </summary>
	/// <returns><c>true</c>, if it is in search space, <c>false</c> otherwise.</returns>
	/// <param name="cameraParams">viewpoint parameters (depend on specific viewpoint representation)</param>
	public virtual bool InSearchSpace (float[] cameraParams) {
		if (minGeometryDistance > 0.000001f) {

			return !(Physics.CheckSphere( new Vector3( cameraParams[0], cameraParams[1], cameraParams[2] ), minGeometryDistance, ~layersToExclude ));
		}
		return true;

	}

	/// <summary>
	/// Computes a random viewpoint inside the domain.
	/// </summary>
	/// <returns>The random viewpoint, according to the specific viewpoint representation</returns>
	/// <param name="dimension">dimensions of the viewpoint (i.e. 3 for just position)</param>
	public abstract float[] ComputeRandomViewpoint (int dimension); 

	/// <summary>
	/// Returns the range between each camera parameter. */
	/// </summary>
	/// <returns>The parameters range.</returns>
	/// <param name="dimension">parameters dimension.</param>
	public abstract float[] GetParametersRange (int dimension);

	/// <summary>
	/// Gets the minimum parameters value in the domain
	/// </summary>
	/// <returns>The minimum parameters.</returns>
	/// <param name="dimension">parameters dimension.</param>
	public abstract float[] GetMinParameters( int dimension);

	/// <summary>
	/// Gets the max parameters value in the domain
	/// </summary>
	/// <returns>The max parameters.</returns>
	/// <param name="dimension">parameters dimension.</param>
	public abstract float[] GetMaxParameters( int dimension);



}

/// <summary>
/// Class defining the domain of a VC problem when camera parameters are expressed as position, lookAt point,
/// roll, YFOV
/// </summary> 
public class VCLookAtProblemDomain : VCProblemDomain
{

	/// <summary>
	/// Position bounds of the VC problem
	/// </summary>
	public Bounds positionBounds;

	/// <summary>
	/// Look-at bounds of the VC problem
	/// </summary>
	public Bounds lookAtBounds;

	/// <summary>
	/// checks if the provided viewpoint is in the problem domain
	/// </summary>
	/// <returns>true</returns>, if it is in search space, <c>false</c> otherwise.</returns>
	/// <param name="cameraParams">viewpoint parameters</param>
	public override bool InSearchSpace (float[] cameraParams)
	{

		bool inSpace = base.InSearchSpace( cameraParams );
		if (cameraParams.Length >= 3) { // i.e. we have just camera position 
			inSpace = positionBounds.Contains (new Vector3 (cameraParams [0], cameraParams [1], cameraParams [2]));
			if (!inSpace)
				return false;
		}
		if (cameraParams.Length >= 6) { // i.e. we have also look at point
			inSpace = inSpace && lookAtBounds.Contains (new Vector3 (cameraParams [3], cameraParams [4], cameraParams [5]));
			if (!inSpace)
				return false;
		}
		if (cameraParams.Length >= 7) { // i.e. we have also roll
			inSpace = inSpace && (rollBounds.x <= cameraParams [6]) && (cameraParams [6] <= rollBounds.y);
			if (!inSpace)
				return false;
		}
		if (cameraParams.Length == 8) { // i.e. we have also fov
			inSpace = inSpace && (yFOVBounds.x <= cameraParams [7]) && (cameraParams [7] <= yFOVBounds.y);
			if (!inSpace)
				return false;
		}

		if (inSpace && minGeometryDistance > 0.0f) {
			inSpace = !(Physics.CheckSphere( new Vector3( cameraParams[0], cameraParams[1], cameraParams[2] ), minGeometryDistance, ~layersToExclude ));
		}

		return inSpace;
	}

	/// <summary>
	/// Computes a random viewpoint inside the domain.
	/// </summary>
	/// <returns>The random viewpoint, according to the specific viewpoint representation</returns>
	/// <param name="dimension">dimensions of the viewpoint (i.e. 3 for just position)</param>
	public override float[] ComputeRandomViewpoint (int dimension) {
		float[] viewpointResult = new float[dimension];
		viewpointResult [0] = UnityEngine.Random.Range (positionBounds.min.x, positionBounds.max.x);	
		viewpointResult [1] = UnityEngine.Random.Range (positionBounds.min.y, positionBounds.max.y);
		viewpointResult [2] = UnityEngine.Random.Range (positionBounds.min.z, positionBounds.max.z);	

		if (dimension >= 4) {
			viewpointResult [3] = UnityEngine.Random.Range (lookAtBounds.min.x, lookAtBounds.max.x);
			viewpointResult [4] = UnityEngine.Random.Range (lookAtBounds.min.y, lookAtBounds.max.y);
			viewpointResult [5] = UnityEngine.Random.Range (lookAtBounds.min.z, lookAtBounds.max.z);
		}
		if (dimension >= 7) {
			viewpointResult [6] = UnityEngine.Random.Range (rollBounds.x, rollBounds.y);
		}
		if (dimension == 8) {
			viewpointResult [7] = UnityEngine.Random.Range (yFOVBounds.x, yFOVBounds.y);
		}

		return viewpointResult;

	}

	/// <summary>
	/// Returns the range between each camera parameter. */
	/// </summary>
	/// <returns>The parameters range.</returns>
	/// <param name="dimension">parameters dimension.</param>
	public override float[] GetParametersRange (int dimension) {
		float[] result = new float[dimension];
		result [0] = positionBounds.size.x;
		result [1] = positionBounds.size.y;
		result [2] = positionBounds.size.z;
		if (dimension > 3) {
			result [3] = lookAtBounds.size.x;
			result [4] = lookAtBounds.size.y;
			result [5] = lookAtBounds.size.z;
		}
		if (dimension > 6) {
			result [6] = rollBounds.y - rollBounds.x;
		}
		if (dimension > 7) {
			result [7] = yFOVBounds.y - yFOVBounds.x;
		}
		return result;
	}

	/// <summary>
	/// Gets the minimum parameters value in the domain
	/// </summary>
	/// <returns>The minimum parameters.</returns>
	/// <param name="dimension">parameters dimension.</param>
	public override float[] GetMinParameters( int dimension) {
		float[] result = new float[dimension];
		result [0] = positionBounds.min.x;
		result [1] = positionBounds.min.y;
		result [2] = positionBounds.min.z;
		if (dimension > 3) {
			result [3] = lookAtBounds.min.x;
			result [4] = lookAtBounds.min.y;
			result [5] = lookAtBounds.min.z;
		}
		if (dimension > 6) {
			result [6] = rollBounds.x;
		}
		if (dimension > 7) {
			result [7] = yFOVBounds.x;
		}
		return result;
	}

	/// <summary>
	/// Gets the max parameters value in the domain
	/// </summary>
	/// <returns>The max parameters.</returns>
	/// <param name="dimension">parameters dimension.</param>
	public override float[] GetMaxParameters( int dimension) {
		float[] result = new float[dimension];
		result [0] = positionBounds.max.x;
		result [1] = positionBounds.max.y;
		result [2] = positionBounds.max.z;
		if (dimension > 3) {
			result [3] = lookAtBounds.max.x;
			result [4] = lookAtBounds.max.y;
			result [5] = lookAtBounds.max.z;
		}
		if (dimension > 6) {
			result [6] = rollBounds.y;
		}
		if (dimension > 7) {
			result [7] = yFOVBounds.y;
		}
		return result;

	}

}

/// <summary>
/// Class defining the domain of a VC problem when camera parameters are expressed as pivot, distance, phi, theta, roll, YFOV
/// </summary> 
public class VCOrbitProblemDomain: VCProblemDomain
{



	/// <summary>
	/// min and max distance from pivot
	/// </summary>
	public Vector2 distanceBounds;

	/// <summary>
	/// min and max polar angle
	/// </summary>
	public Vector2 thetaBounds;

	/// <summary>
	/// min and max azimuthal angle
	/// </summary>
	public Vector2 phiBounds;

	/// <summary>
	/// checks if the provided viewpoint is in the problem domain
	/// </summary>
	/// <returns>true</returns>, if it is in search space, <c>false</c> otherwise.</returns>
	/// <param name="parameters">viewpoint parameters</param>
	public override bool InSearchSpace (float[] parameters)
	{

		bool inSpace = base.InSearchSpace( parameters );
		inSpace = ((distanceBounds.x <= parameters [0]) && (parameters [0] <= distanceBounds.y) &&
			(thetaBounds.x <= parameters [1]) && (parameters [1] <= thetaBounds.y) &&
			(phiBounds.x <= parameters [2]) && (parameters [2] <= phiBounds.y));

		if (!inSpace)
			return false;

		if (parameters.Length >= 4) {  // i.e. there is roll
			inSpace = inSpace && (rollBounds.x <= parameters [3]) && (parameters [3] <= rollBounds.y);
			if (!inSpace)
				return false;
		}

		if (parameters.Length >=5) {  // i.e. there is fov
			inSpace = inSpace && (yFOVBounds.x <= parameters [4]) && (parameters [4] <= yFOVBounds.y);
			if (!inSpace)
				return false;
		}

		if (inSpace && minGeometryDistance > 0.0f) {

			inSpace = !(Physics.CheckSphere( new Vector3( parameters[0], parameters[1], parameters[2] ), minGeometryDistance, ~layersToExclude ));

		}
		return inSpace;
	}

	/// <summary>
	/// Computes a random viewpoint inside the domain.
	/// </summary>
	/// <returns>The random viewpoint, according to the specific viewpoint representation</returns>
	/// <param name="dimension">dimensions of the viewpoint (i.e. 3 for just position)</param>
	public override float[] ComputeRandomViewpoint (int dimension) {
		float[] viewpointResult = new float[dimension];
		viewpointResult [0] = UnityEngine.Random.Range (distanceBounds.x, distanceBounds.y);

		if (dimension > 1) { // theta and phi go together
			viewpointResult [1] = UnityEngine.Random.Range (thetaBounds.x, thetaBounds.y);
			viewpointResult [2] = UnityEngine.Random.Range (phiBounds.x, phiBounds.y);	
		}

		if (dimension > 3) {
			viewpointResult [3] = UnityEngine.Random.Range (rollBounds.x, rollBounds.y);
		}

		if (dimension > 4) {
			viewpointResult [4] = UnityEngine.Random.Range (yFOVBounds.x, yFOVBounds.y);
		}

		return viewpointResult;

	}

	/// <summary>
	/// Returns the range between each camera parameter. */
	/// </summary>
	/// <returns>The parameters range.</returns>
	/// <param name="dimension">parameters dimension.</param>
	public override float[] GetParametersRange( int dimension ) {
		float[] result = new float[dimension];
		result [0] = distanceBounds.y - distanceBounds.x;
		if (dimension > 1) {
			result [1] = thetaBounds.y - thetaBounds.x;
			result [2] = phiBounds.y - phiBounds.x;
		}
		if (dimension > 3) {
			result [3] = rollBounds.y - rollBounds.x;
		}
		if (dimension > 4) {
			result [4] = yFOVBounds.y - yFOVBounds.x;
		}
		return result;

	} 

	/// <summary>
	/// Gets the minimum parameters value in the domain
	/// </summary>
	/// <returns>The minimum parameters.</returns>
	/// <param name="dimension">parameters dimension.</param>
	public override float[] GetMinParameters( int dimension) {
		float[] result = new float[dimension];
		result [0] = distanceBounds.x;
		if (dimension > 1) {
			result [1] = thetaBounds.x;
			result [2] = phiBounds.x;
		}
		if (dimension > 3) {
			result [3] = rollBounds.x;
		}
		if (dimension > 4) {
			result [4] = yFOVBounds.x;
		}
		return result;

	}

	/// <summary>
	/// Gets the max parameters value in the domain
	/// </summary>
	/// <returns>The max parameters.</returns>
	/// <param name="dimension">parameters dimension.</param>
	public override float[] GetMaxParameters( int dimension) {
		float[] result = new float[dimension];
		result [0] = distanceBounds.y;
		if (dimension > 1) {
			result [1] = thetaBounds.y;
			result [2] = phiBounds.y;
		}
		if (dimension > 3) {
			result [3] = rollBounds.y;
		}
		if (dimension > 4) {
			result [4] = yFOVBounds.y;
		}
		return result;

	}

}


