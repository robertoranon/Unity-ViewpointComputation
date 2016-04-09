/*
-----------------------------------------------------------------------------
This source file is part of ViewpointComputationLib (a viewpoint computation library)
For more info on the project, contact Roberto Ranon at roberto.ranon@uniud.it.

Copyright (c) 2013- University of Udine, Italy - http://hcilab.uniud.it
Also see acknowledgements in readme.txt
-----------------------------------------------------------------------------

 CLViewpoint.cs: file defining classes to represent a viewpoint, i.e. a solution of a VC problem

-----------------------------------------------------------------------------
*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;


/// <summary>
/// CLViewpoint represents a viewpoint (e.g. computed by our camera man), with its objective function and computed satisfaction
/// </summary>
public class CLViewpoint
{
	
	
	/// <summary>
	/// The viewpoint position.
	/// </summary>
	public Vector3 position;
	
	/// <summary>
	/// The viewpoint look-at point.
	/// </summary>
	public Vector3 lookAtPoint;
	
	/// <summary>
	/// The viewpoint rotation.
	/// </summary>
	public Quaternion rotation;
	
	/// <summary>
	/// The viewpoint yFOV (not used at the moment)
	/// </summary>
	public float yFOV;

	/// <summary>
	/// True if the viewpoint, when last evaluated, was inside the defined search space
	/// </summary>
	public bool inSearchSpace;

	/// <summary>
	/// List of visual properties that the viewpoint should satisfy. The first one is the
	/// satisfaction function, the others are its components
	/// </summary>
	public List<CLVisualProperty> properties;

	/// <summary>
	/// The satisfaction of each visual property in [0,1].
	/// </summary>
	public List<float> satisfaction;
	
	/// <summary>
	/// The in screen ratio of each visual property in [0,1].
	/// </summary>
	public List<float> inScreenRatio;

	/// <summary>
	/// The pso representation of the viewpoint (e.g. 3 floats for position, 3 for look at point, 1 for roll, 1 for FOV)
	/// </summary>
	public float[] psoRepresentation;
	

	/// <summary>
	/// Evaluates the viewpoint using the provided camera man 
	/// </summary>
	/// <returns>The viewpoint satisfaction in [0,1].</returns>
	/// <param name="camera">camera man object</param>
	public float EvaluateViewpoint( CLCameraMan cameraman, bool setObjectiveFunction = false ) {

		if (setObjectiveFunction) {

			cameraman.SetSpecification( properties);

		}

		cameraman.unityCamera.transform.position = this.position;
		cameraman.unityCamera.transform.rotation = this.rotation;

		foreach (CLVisualProperty p in cameraman.properties) {
			p.evaluated = false;
		}
		foreach (CLTarget t in cameraman.targets) {
			t.rendered = false;
		}

		return properties[0].EvaluateSatisfaction( cameraman );
	}





}
